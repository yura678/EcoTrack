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
/// Tests the fan-out behavior: for each matching subscription, one
/// <see cref="PerSubscriptionNotificationDispatcher"/> Hangfire job should be enqueued. The
/// fan-out dispatcher no longer sends anything inline — actual delivery (and HMAC, retry,
/// idempotency, etc.) is covered by <c>PerSubscriptionNotificationDispatcherTests</c>.
/// </summary>
public class ComplianceNotificationDispatcherTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly MonitoringDevice _device;

    public ComplianceNotificationDispatcherTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);
    }

    [Fact]
    public async Task ShouldEnqueuePerSubJobForMatchingSubscription()
    {
        var sub = NotificationSubscription.NewEmail(
            id: Guid.NewGuid(),
            userId: TestAuthHandler.TestUserId,
            email: "ops@example.com",
            eventTypes: [ComplianceEventType.OutOfRangeReading],
            emissionSourceIds: null);
        sub.AssignTenant(_enterprise.Id);
        await Context.Set<NotificationSubscription>().AddAsync(sub);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, ratio: 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow,
            notes: "12/60 readings (20%) out of sensor range");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        await DispatchAsync(ev.Id);

        var perSub = _factory.Jobs.Created
            .Where(c => c.Job.Method.DeclaringType == typeof(PerSubscriptionNotificationDispatcher))
            .ToList();
        perSub.Should().HaveCount(1);
        perSub.Single().Job.Args[0].Should().Be(ev.Id);
        perSub.Single().Job.Args[1].Should().Be(sub.Id);
    }

    [Fact]
    public async Task ShouldNotEnqueueForUnmatchedEventTypeFilter()
    {
        var sub = NotificationSubscription.NewEmail(
            Guid.NewGuid(), TestAuthHandler.TestUserId, "ops@example.com",
            eventTypes: [ComplianceEventType.DeviceOffline],
            emissionSourceIds: null);
        sub.AssignTenant(_enterprise.Id);
        await Context.Set<NotificationSubscription>().AddAsync(sub);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        await DispatchAsync(ev.Id);

        PerSubJobs().Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldNotEnqueueForUnmatchedSourceFilter()
    {
        var otherSourceId = Guid.NewGuid();
        var sub = NotificationSubscription.NewEmail(
            Guid.NewGuid(), TestAuthHandler.TestUserId, "ops@example.com",
            eventTypes: null,
            emissionSourceIds: [otherSourceId]);
        sub.AssignTenant(_enterprise.Id);
        await Context.Set<NotificationSubscription>().AddAsync(sub);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        await DispatchAsync(ev.Id);

        PerSubJobs().Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldNotEnqueueForDisabledSubscription()
    {
        var sub = NotificationSubscription.NewEmail(
            Guid.NewGuid(), TestAuthHandler.TestUserId, "ops@example.com",
            eventTypes: null, emissionSourceIds: null);
        sub.AssignTenant(_enterprise.Id);
        sub.UpdateFilters(null, null, isEnabled: false);
        await Context.Set<NotificationSubscription>().AddAsync(sub);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        await DispatchAsync(ev.Id);

        PerSubJobs().Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldEnqueueOneJobPerMatchingSubscriptionAcrossChannels()
    {
        var emailSub = NotificationSubscription.NewEmail(
            Guid.NewGuid(), TestAuthHandler.TestUserId, "ops@example.com",
            eventTypes: null, emissionSourceIds: null);
        emailSub.AssignTenant(_enterprise.Id);
        var webhookSub = NotificationSubscription.NewWebhook(
            Guid.NewGuid(), TestAuthHandler.TestUserId,
            "https://hooks.example.com/eco", "0123456789abcdef0123456789abcdef",
            eventTypes: null, emissionSourceIds: null);
        webhookSub.AssignTenant(_enterprise.Id);
        await Context.Set<NotificationSubscription>().AddAsync(emailSub);
        await Context.Set<NotificationSubscription>().AddAsync(webhookSub);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        await DispatchAsync(ev.Id);

        var perSub = PerSubJobs();
        perSub.Should().HaveCount(2);
        perSub.Select(j => (Guid)j.Job.Args[1]!).Should()
            .BeEquivalentTo([emailSub.Id, webhookSub.Id]);
    }

    private List<CreatedJob> PerSubJobs() => _factory.Jobs.Created
        .Where(c => c.Job.Method.DeclaringType == typeof(PerSubscriptionNotificationDispatcher))
        .ToList();

    private async Task DispatchAsync(Guid eventId)
    {
        _factory.Jobs.Clear();
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
