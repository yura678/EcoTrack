using System.Net;
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
/// Per-(subscription, event) delivery — the Phase B inner job. Each test drives the
/// dispatcher directly to assert on the <see cref="NotificationDelivery"/> audit row that
/// guarantees "we know whether subscriber X was notified about event Y".
/// </summary>
public class PerSubscriptionNotificationDispatcherTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly MonitoringDevice _device;

    public PerSubscriptionNotificationDispatcherTests(IntegrationTestWebFactory factory)
        : base(factory)
    {
        _factory = factory;
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);
    }

    [Fact]
    public async Task ShouldSendEmailAndMarkDeliveryDelivered()
    {
        var sub = NotificationSubscription.NewEmail(
            Guid.NewGuid(), TestAuthHandler.TestUserId, "ops@example.com",
            eventTypes: null, emissionSourceIds: null);
        sub.AssignTenant(_enterprise.Id);
        await Context.Set<NotificationSubscription>().AddAsync(sub);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow,
            notes: "12/60 readings (20%) out of sensor range");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        _factory.Emails.Clear();
        await DispatchAsync(ev.Id, sub.Id);

        _factory.Emails.Sent.Should().HaveCount(1);
        var email = _factory.Emails.Sent.First();
        email.To.Should().Be("ops@example.com");
        email.Subject.Should().Contain("Показники поза діапазоном");
        email.Body.Should().Contain(_source.Id.ToString());

        var delivery = await GetDeliveryAsync(sub.Id, ev.Id);
        delivery.Should().NotBeNull();
        delivery!.Status.Should().Be(NotificationDeliveryStatus.Delivered);
        delivery.Channel.Should().Be(NotificationChannel.Email);
        delivery.EnterpriseId.Should().Be(_enterprise.Id);
        delivery.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ShouldShortCircuitWhenDeliveryAlreadyDelivered()
    {
        var sub = NotificationSubscription.NewEmail(
            Guid.NewGuid(), TestAuthHandler.TestUserId, "ops@example.com",
            null, null);
        sub.AssignTenant(_enterprise.Id);
        await Context.Set<NotificationSubscription>().AddAsync(sub);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        _factory.Emails.Clear();
        await DispatchAsync(ev.Id, sub.Id);
        _factory.Emails.Sent.Should().HaveCount(1);

        // Simulate Hangfire re-running the same job. Idempotency comes from the unique
        // (SubscriptionId, ComplianceEventId) constraint + the Delivered short-circuit; a
        // second send would be observable as a second email in the queue.
        _factory.Emails.Clear();
        await DispatchAsync(ev.Id, sub.Id);
        _factory.Emails.Sent.Should().BeEmpty(
            "re-run of an already-Delivered job must not double-send");
    }

    [Fact]
    public async Task ShouldRetainPendingRowAndIncrementAttemptCountOnRetryThenSuccess()
    {
        var sub = NotificationSubscription.NewWebhook(
            Guid.NewGuid(), TestAuthHandler.TestUserId,
            "https://hooks.example.com/eco", "0123456789abcdef0123456789abcdef",
            eventTypes: null, emissionSourceIds: null);
        sub.AssignTenant(_enterprise.Id);
        await Context.Set<NotificationSubscription>().AddAsync(sub);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        // Attempt #1: subscriber returns 500 → MarkAttempt + throw.
        _factory.WebhookHttp.Clear();
        _factory.WebhookHttp.StatusCode = HttpStatusCode.InternalServerError;
        var firstAttempt = async () => await DispatchAsync(ev.Id, sub.Id);
        await firstAttempt.Should().ThrowAsync<HttpRequestException>();

        var afterFailure = await GetDeliveryAsync(sub.Id, ev.Id);
        afterFailure!.Status.Should().Be(NotificationDeliveryStatus.Pending);
        afterFailure.AttemptCount.Should().Be(1);

        // Attempt #2 (Hangfire retry): subscriber returns 200 → MarkDelivered, same row.
        _factory.WebhookHttp.Clear();
        _factory.WebhookHttp.StatusCode = HttpStatusCode.OK;
        await DispatchAsync(ev.Id, sub.Id);

        var afterSuccess = await GetDeliveryAsync(sub.Id, ev.Id);
        afterSuccess!.Id.Should().Be(afterFailure.Id,
            "the retry must reuse the existing delivery row, not create a new one — " +
            "(subscription_id, compliance_event_id) is the unique idempotency key");
        afterSuccess.Status.Should().Be(NotificationDeliveryStatus.Delivered);
        afterSuccess.AttemptCount.Should().Be(1,
            "AttemptCount counts failures only; the success path does not bump it");
        afterSuccess.LastError.Should().BeNull("MarkDelivered clears the previous error");
        afterSuccess.DeliveredAt.Should().NotBeNull();
    }

    private async Task<NotificationDelivery?> GetDeliveryAsync(Guid subId, Guid eventId) =>
        await Context.Set<NotificationDelivery>().AsNoTracking()
            .FirstOrDefaultAsync(d =>
                d.SubscriptionId == subId && d.ComplianceEventId == eventId);

    private async Task DispatchAsync(Guid eventId, Guid subId)
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
