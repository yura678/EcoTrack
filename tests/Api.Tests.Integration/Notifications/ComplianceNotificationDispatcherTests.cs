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
    public async Task ShouldSendEmailToMatchingSubscription()
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

        _factory.Emails.Sent.Should().HaveCount(1);
        var email = _factory.Emails.Sent.First();
        email.To.Should().Be("ops@example.com");
        email.Subject.Should().Contain("Показники поза діапазоном");
        email.Body.Should().Contain(_source.Id.ToString());
        email.Body.Should().Contain("12/60 readings (20%) out of sensor range");
    }

    [Fact]
    public async Task ShouldSkipSubscriptionWithUnmatchedEventTypeFilter()
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

        _factory.Emails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldSkipSubscriptionWithUnmatchedSourceFilter()
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

        _factory.Emails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldSkipDisabledSubscription()
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

        _factory.Emails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldNotSendEmailForWebhookChannelSubscription()
    {
        var sub = NotificationSubscription.NewWebhook(
            Guid.NewGuid(), TestAuthHandler.TestUserId,
            webhookUrl: "https://hooks.example.com/eco",
            webhookSecret: "0123456789abcdef0123456789abcdef");
        sub.AssignTenant(_enterprise.Id);
        await Context.Set<NotificationSubscription>().AddAsync(sub);

        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        await DispatchAsync(ev.Id);

        _factory.Emails.Sent.Should().BeEmpty();
    }

    private async Task DispatchAsync(Guid eventId)
    {
        // Clear queue from prior test interaction within the same factory instance.
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
