using System.Net;
using Api.Dtos;
using Application.Common.Models;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using FluentAssertions;
using Tests.Common;
using Tests.Data.EmissionSources;
using Tests.Data.Enterprises;
using Tests.Data.Monitoring;

namespace Api.Tests.Integration.Compliance;

public class ScopedComplianceEventListTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installationA;
    private readonly Installation _installationB;
    private readonly EmissionSource _sourceA;
    private readonly EmissionSource _sourceB;
    private readonly Pollutant _pollutant;
    private readonly MonitoringDevice _deviceA;
    private readonly MonitoringDevice _deviceB;

    public ScopedComplianceEventListTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installationA = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _installationB = InstallationData.SecondTestInstallation(_site.Id, _iedCategory.Id);
        _sourceA = EmissionSourcesData.FirstTestEmissionSource(_installationA.Id);
        _sourceB = EmissionSourcesData.SecondTestAirEmissionSource(_installationB.Id);
        _pollutant = PollutantsData.FirstTestPollutant();
        _deviceA = MonitoringDevicesData.FirstTestDevice(_sourceA.Id, _installationA.Id);
        _deviceB = MonitoringDevicesData.SecondTestDevice(_sourceB.Id, _installationB.Id);
    }

    [Fact]
    public async Task ShouldReturnOnlyEventsBelongingToInstallation()
    {
        var (eventA, eventB) = await SeedOneEventPerSourceAsync();

        var listA = await GetPagedAsync($"installations/{_installationA.Id}/compliance-events");
        listA.Items.Should().HaveCount(1);
        listA.Items[0].Id.Should().Be(eventA.Id);

        var listB = await GetPagedAsync($"installations/{_installationB.Id}/compliance-events");
        listB.Items.Should().HaveCount(1);
        listB.Items[0].Id.Should().Be(eventB.Id);
    }

    [Fact]
    public async Task ShouldReturnAllSiteEventsAcrossInstallations()
    {
        var (eventA, eventB) = await SeedOneEventPerSourceAsync();

        var list = await GetPagedAsync($"sites/{_site.Id}/compliance-events");
        list.TotalCount.Should().Be(2);
        list.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { eventA.Id, eventB.Id });
    }

    [Fact]
    public async Task ShouldCombineInstallationScopeWithStatusFilter()
    {
        // Two events on installationA — one Open, one Closed. status=Open must return one row.
        var open = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), _sourceA.Id, _deviceA.Id,
            windowStart: DateTime.UtcNow.AddHours(-3),
            windowEnd: DateTime.UtcNow.AddHours(-2),
            notes: "still open");
        var closed = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), _sourceA.Id, _deviceA.Id,
            windowStart: DateTime.UtcNow.AddHours(-5),
            windowEnd: DateTime.UtcNow.AddHours(-4),
            notes: "old");
        closed.Close(ResolutionReason.SensorFault, "lab returned ok",
            resolvedByUserId: Guid.NewGuid());
        await Context.Set<ComplianceEvent>().AddRangeAsync(open, closed);
        await SaveChangesAsync();

        var list = await GetPagedAsync(
            $"installations/{_installationA.Id}/compliance-events?status={ComplianceEventStatus.Open}");
        list.TotalCount.Should().Be(1);
        list.Items[0].Id.Should().Be(open.Id);
    }

    [Fact]
    public async Task ShouldReturnEmptyForInstallationOutsideSite()
    {
        await SeedOneEventPerSourceAsync();

        var unrelatedInstallationId = Guid.NewGuid();
        var list = await GetPagedAsync($"installations/{unrelatedInstallationId}/compliance-events");
        list.TotalCount.Should().Be(0);
        list.Items.Should().BeEmpty();
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<List<ComplianceEvent>> AddAsync(params ComplianceEvent[] events)
    {
        await Context.Set<ComplianceEvent>().AddRangeAsync(events);
        await SaveChangesAsync();
        return events.ToList();
    }

    private async Task<(ComplianceEvent A, ComplianceEvent B)> SeedOneEventPerSourceAsync()
    {
        var eventA = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), _sourceA.Id, _deviceA.Id,
            windowStart: DateTime.UtcNow.AddHours(-2),
            windowEnd: DateTime.UtcNow.AddHours(-1),
            notes: "A offline");
        var eventB = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), _sourceB.Id, _deviceB.Id,
            windowStart: DateTime.UtcNow.AddHours(-2),
            windowEnd: DateTime.UtcNow.AddHours(-1),
            notes: "B offline");
        await AddAsync(eventA, eventB);
        return (eventA, eventB);
    }

    private async Task<PageResult<ComplianceEventDto>> GetPagedAsync(string path)
    {
        var response = await Client.GetAsync($"api/v1/{path}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ToResponseModel<PageResult<ComplianceEventDto>>();
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddRangeAsync(_installationA, _installationB);
        await Context.Set<EmissionSource>().AddRangeAsync(_sourceA, _sourceB);
        await Context.Set<Pollutant>().AddAsync(_pollutant);
        await Context.Set<MonitoringDevice>().AddRangeAsync(_deviceA, _deviceB);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
