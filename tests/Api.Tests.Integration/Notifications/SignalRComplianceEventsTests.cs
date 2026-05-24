using Api.Dtos;
using Api.Hubs;
using Application.Features.ComplianceEvents.Notifications;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.EmissionSources;
using Tests.Data.Enterprises;
using Tests.Data.Monitoring;

namespace Api.Tests.Integration.Notifications;

public class SignalRComplianceEventsTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly MonitoringDevice _device;

    public SignalRComplianceEventsTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
        // EnterpriseId MUST match the JWT's CompanyId claim because the hub groups by it.
        _enterprise = Enterprise.NewActive(
            TestAuthHandler.TestCompanyId,
            "SignalR test enterprise",
            edrpou: "99999999",
            address: "test",
            riskGroup: RiskGroup.Average,
            sectorId: _sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);
    }

    [Fact]
    public async Task ShouldPushOpenedEventToConnectedBrowserOfSameEnterprise()
    {
        var received = new TaskCompletionSource<ComplianceEventDto>();
        await using var connection = await BuildConnectionAsync(roleOverride: null);
        connection.On<ComplianceEventDto>(
            ComplianceEventsHub.EventOpenedMethod,
            dto => received.TrySetResult(dto));
        await connection.StartAsync();

        var ev = await SeedComplianceEventAsync();
        await PublishOpenedAsync(ev.Id);

        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pushed.Id.Should().Be(ev.Id);
        pushed.EventType.Should().Be(ComplianceEventType.OutOfRangeReading);
        pushed.EmissionSourceId.Should().Be(_source.Id);
    }

    [Fact]
    public async Task ShouldPushClosedEventToConnectedBrowserOfSameEnterprise()
    {
        // Symmetric to the opened-event push: closes (manual or detector auto-close) must reach
        // browsers reactively too — otherwise dashboards show stale Open state until reload.
        var received = new TaskCompletionSource<ComplianceEventDto>();
        await using var connection = await BuildConnectionAsync(roleOverride: null);
        connection.On<ComplianceEventDto>(
            ComplianceEventsHub.EventClosedMethod,
            dto => received.TrySetResult(dto));
        await connection.StartAsync();

        var ev = await SeedComplianceEventAsync();
        ev.Close(ResolutionReason.OperatorAction, "auto-close smoke test", resolvedByUserId: null);
        // SeedComplianceEventAsync's SaveChangesAsync calls ChangeTracker.Clear(); the returned
        // entity is detached. Re-attach as Modified so the Close() mutation reaches the DB.
        Context.Set<ComplianceEvent>().Update(ev);
        await SaveChangesAsync();
        await PublishClosedAsync(ev.Id);

        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pushed.Id.Should().Be(ev.Id);
        pushed.Status.Should().Be(ComplianceEventStatus.Closed);
    }

    [Fact]
    public async Task ShouldNotPushToUserWithoutCompliancePermission()
    {
        // Non-superAdmin role with no DynamicPermission claims → CanAccess returns false in
        // OnConnectedAsync, so the connection is established but never joins the group.
        var received = new TaskCompletionSource<ComplianceEventDto>();
        await using var connection = await BuildConnectionAsync(roleOverride: "BillingViewer");
        connection.On<ComplianceEventDto>(
            ComplianceEventsHub.EventOpenedMethod,
            dto => received.TrySetResult(dto));
        await connection.StartAsync();

        var ev = await SeedComplianceEventAsync();
        await PublishOpenedAsync(ev.Id);

        var winner = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        winner.Should().NotBe(received.Task,
            "the user without permission must stay silent while the broadcast fans out");
        received.Task.IsCompleted.Should().BeFalse();
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<HubConnection> BuildConnectionAsync(string? roleOverride)
    {
        var server = _factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "hubs/compliance"), options =>
            {
                // Route the SignalR client through TestServer's in-process HTTP handler.
                // Long-polling-only because TestServer doesn't surface a real WebSocket.
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Authorization"] = "TestScheme";
                if (!string.IsNullOrEmpty(roleOverride))
                {
                    options.Headers[TestAuthHandler.RoleOverrideHeader] = roleOverride;
                }
            })
            .Build();
        return connection;
    }

    private async Task<ComplianceEvent> SeedComplianceEventAsync()
    {
        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), _source.Id, _device.Id, 0.20m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow,
            notes: "live test");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();
        return ev;
    }

    private async Task PublishOpenedAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(new ComplianceEventOpenedNotification(eventId), CancellationToken.None);
    }

    private async Task PublishClosedAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(new ComplianceEventClosedNotification(eventId), CancellationToken.None);
    }

    public async Task InitializeAsync()
    {
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
