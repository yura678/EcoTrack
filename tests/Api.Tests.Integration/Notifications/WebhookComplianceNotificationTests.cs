using System.Net;
using System.Security.Cryptography;
using System.Text;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Domain.Entities.Notifications;
using FluentAssertions;
using Infrastructure.Compliance.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.EmissionSources;
using Tests.Data.Enterprises;
using Tests.Data.Monitoring;
using UserEntity = Domain.Entities.User.User;

namespace Api.Tests.Integration.Notifications;

public class WebhookComplianceNotificationTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly MonitoringDevice _device;

    private const string WebhookUrl = "https://hooks.example.com/eco";
    private const string WebhookSecret = "0123456789abcdef0123456789abcdef";

    public WebhookComplianceNotificationTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);
    }

    [Fact]
    public async Task ShouldPostSignedPayloadToWebhookSubscriber()
    {
        await SeedWebhookSubscriptionAsync(WebhookUrl, WebhookSecret,
            eventTypes: [ComplianceEventType.OutOfRangeReading]);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow,
            notes: "12/60 readings out of range");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        await DispatchAsync(ev.Id);

        _factory.WebhookHttp.Requests.Should().HaveCount(1);
        var captured = _factory.WebhookHttp.Requests.First();
        captured.Method.Should().Be(HttpMethod.Post);
        captured.Uri.AbsoluteUri.Should().Be(WebhookUrl);

        // Body is JSON payload — must contain the event id and source id.
        captured.Body.Should().Contain(ev.Id.ToString());
        captured.Body.Should().Contain(_source.Id.ToString());
        captured.Body.Should().Contain("OutOfRangeReading");

        // HMAC headers must be present and signature must verify against the body.
        var signature = captured.Headers.GetValues("X-Signature").Single();
        var timestamp = captured.Headers.GetValues("X-Timestamp").Single();
        var nonce = captured.Headers.GetValues("X-Nonce").Single();

        var canonical = $"{timestamp}.{nonce}.{captured.Body}";
        var expected = Convert.ToBase64String(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret), Encoding.UTF8.GetBytes(canonical)));
        signature.Should().Be(expected,
            "subscribers verify our calls by recomputing HMAC over '{ts}.{nonce}.{body}' — " +
            "any mismatch means valid payloads would be rejected as forged");
    }

    [Fact]
    public async Task ShouldContinueDispatchWhenWebhookEndpointReturnsServerError()
    {
        _factory.WebhookHttp.StatusCode = HttpStatusCode.InternalServerError;
        await SeedWebhookSubscriptionAsync(WebhookUrl, WebhookSecret, eventTypes: null);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        // DispatchAsync swallows per-recipient errors. The job itself completes without throw;
        // Hangfire would normally schedule a retry on uncaught exceptions, but here we want
        // confirmation that one bad subscriber doesn't crash the dispatcher.
        var act = async () => await DispatchAsync(ev.Id);
        await act.Should().NotThrowAsync();

        _factory.WebhookHttp.Requests.Should().HaveCount(1,
            "the attempt was still made and captured even though the endpoint returned 500");
    }

    [Fact]
    public async Task ShouldDeliverToBothEmailAndWebhookSubscribers()
    {
        await SeedEmailSubscriptionAsync("ops@example.com", eventTypes: null);
        await SeedWebhookSubscriptionAsync(WebhookUrl, WebhookSecret, eventTypes: null);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        await DispatchAsync(ev.Id);

        _factory.Emails.Sent.Should().HaveCount(1);
        _factory.WebhookHttp.Requests.Should().HaveCount(1);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task SeedEmailSubscriptionAsync(
        string email, ComplianceEventType[]? eventTypes)
    {
        var sub = NotificationSubscription.NewEmail(
            Guid.NewGuid(), TestAuthHandler.TestUserId, email, eventTypes, null);
        sub.AssignTenant(_enterprise.Id);
        await Context.Set<NotificationSubscription>().AddAsync(sub);
        await SaveChangesAsync();
    }

    private async Task SeedWebhookSubscriptionAsync(
        string url, string secret, ComplianceEventType[]? eventTypes)
    {
        var sub = NotificationSubscription.NewWebhook(
            Guid.NewGuid(), TestAuthHandler.TestUserId, url, secret, eventTypes, null);
        sub.AssignTenant(_enterprise.Id);
        await Context.Set<NotificationSubscription>().AddAsync(sub);
        await SaveChangesAsync();
    }

    private async Task DispatchAsync(Guid eventId)
    {
        _factory.WebhookHttp.Clear();
        _factory.Emails.Clear();
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ComplianceNotificationDispatcher>();
        await dispatcher.DispatchAsync(eventId, CancellationToken.None);
    }

    public async Task InitializeAsync()
    {
        var exists = await Context.Set<UserEntity>()
            .AnyAsync(u => u.Id == TestAuthHandler.TestUserId);
        if (!exists)
        {
            var user = new UserEntity
            {
                Id = TestAuthHandler.TestUserId,
                UserName = $"test-{TestAuthHandler.TestUserId:N}",
                NormalizedUserName = $"TEST-{TestAuthHandler.TestUserId:N}".ToUpperInvariant(),
                Email = "test@example.com",
                NormalizedEmail = "TEST@EXAMPLE.COM",
                SecurityStamp = Guid.NewGuid().ToString(),
                EmailConfirmed = true
            };
            await Context.Set<UserEntity>().AddAsync(user);
        }
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
