using System.Net;
using System.Net.Http;
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

/// <summary>
/// Webhook delivery is exercised directly via <see cref="PerSubscriptionNotificationDispatcher"/>
/// because that's where the HTTP POST lives after the Phase B fan-out split. These tests cover
/// the wire-level contract (signed payload, HMAC) and the per-(sub, event) audit trail in
/// <see cref="NotificationDelivery"/>.
/// </summary>
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
    public async Task ShouldPostSignedPayloadAndMarkDeliveryDelivered()
    {
        var sub = await SeedWebhookSubscriptionAsync(WebhookUrl, WebhookSecret,
            eventTypes: [ComplianceEventType.OutOfRangeReading]);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow,
            notes: "12/60 readings out of range");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        _factory.WebhookHttp.Clear();
        await PerSubDispatchAsync(ev.Id, sub.Id);

        _factory.WebhookHttp.Requests.Should().HaveCount(1);
        var captured = _factory.WebhookHttp.Requests.First();
        captured.Method.Should().Be(HttpMethod.Post);
        captured.Uri.AbsoluteUri.Should().Be(WebhookUrl);

        captured.Body.Should().Contain(ev.Id.ToString());
        captured.Body.Should().Contain(_source.Id.ToString());
        captured.Body.Should().Contain("OutOfRangeReading");

        var signature = captured.Headers.GetValues("X-Signature").Single();
        var timestamp = captured.Headers.GetValues("X-Timestamp").Single();
        var nonce = captured.Headers.GetValues("X-Nonce").Single();

        var canonical = $"{timestamp}.{nonce}.{captured.Body}";
        var expected = Convert.ToBase64String(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret), Encoding.UTF8.GetBytes(canonical)));
        signature.Should().Be(expected,
            "subscribers verify our calls by recomputing HMAC over '{ts}.{nonce}.{body}' — " +
            "any mismatch means valid payloads would be rejected as forged");

        var delivery = await GetDeliveryAsync(sub.Id, ev.Id);
        delivery.Should().NotBeNull();
        delivery!.Status.Should().Be(NotificationDeliveryStatus.Delivered);
        delivery.AttemptCount.Should().Be(0,
            "Phase B counts only failed attempts; success path skips MarkAttempt");
        delivery.DeliveredAt.Should().NotBeNull();
        delivery.LastError.Should().BeNull();
        delivery.Channel.Should().Be(NotificationChannel.Webhook);
    }

    [Fact]
    public async Task ShouldMarkAttemptAndThrowWhenWebhookEndpointReturnsServerError()
    {
        var sub = await SeedWebhookSubscriptionAsync(WebhookUrl, WebhookSecret, eventTypes: null);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        // Configure the failure mode AFTER any prior Clear — CapturingHttpMessageHandler.Clear
        // resets StatusCode to OK, so setting it before any helper call would be a no-op.
        _factory.WebhookHttp.Clear();
        _factory.WebhookHttp.StatusCode = HttpStatusCode.InternalServerError;

        // Per-sub dispatcher MUST throw on downstream failure — Hangfire's AutomaticRetry
        // schedule is the whole point of the Phase B split. Phase A swallowed this.
        var act = async () => await PerSubDispatchAsync(ev.Id, sub.Id);
        await act.Should().ThrowAsync<HttpRequestException>();

        _factory.WebhookHttp.Requests.Should().HaveCount(1);

        var delivery = await GetDeliveryAsync(sub.Id, ev.Id);
        delivery.Should().NotBeNull();
        delivery!.Status.Should().Be(NotificationDeliveryStatus.Pending);
        delivery.AttemptCount.Should().Be(1);
        delivery.FirstAttemptedAt.Should().NotBeNull();
        delivery.LastAttemptedAt.Should().NotBeNull();
        delivery.LastError.Should().NotBeNullOrEmpty();
        delivery.DeliveredAt.Should().BeNull();
    }

    private async Task<NotificationSubscription> SeedWebhookSubscriptionAsync(
        string url, string secret, ComplianceEventType[]? eventTypes)
    {
        var sub = NotificationSubscription.NewWebhook(
            Guid.NewGuid(), TestAuthHandler.TestUserId, url, secret, eventTypes, null);
        sub.AssignTenant(_enterprise.Id);
        await Context.Set<NotificationSubscription>().AddAsync(sub);
        await SaveChangesAsync();
        return sub;
    }

    private async Task<NotificationDelivery?> GetDeliveryAsync(Guid subId, Guid eventId) =>
        await Context.Set<NotificationDelivery>().AsNoTracking()
            .FirstOrDefaultAsync(d =>
                d.SubscriptionId == subId && d.ComplianceEventId == eventId);

    private async Task PerSubDispatchAsync(Guid eventId, Guid subId)
    {
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider
            .GetRequiredService<PerSubscriptionNotificationDispatcher>();
        await dispatcher.DispatchAsync(eventId, subId, CancellationToken.None);
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
