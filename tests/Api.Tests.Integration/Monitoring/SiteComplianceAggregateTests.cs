using System.Net;
using Api.Dtos;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using FluentAssertions;
using Tests.Common;
using Tests.Data.EmissionSources;
using Tests.Data.Enterprises;
using Tests.Data.Monitoring;

namespace Api.Tests.Integration.Monitoring;

public class SiteComplianceAggregateTests : BaseIntegrationTest, IAsyncLifetime
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
    private readonly MeasureUnit _kgh;
    private readonly MonitoringDevice _deviceA;
    private readonly MonitoringDevice _deviceB;

    private readonly DateTime _windowStart;
    private readonly DateTime _windowEnd;

    public SiteComplianceAggregateTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installationA = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _installationB = InstallationData.SecondTestInstallation(_site.Id, _iedCategory.Id);
        _sourceA = EmissionSourcesData.FirstTestEmissionSource(_installationA.Id);
        _sourceB = EmissionSourcesData.SecondTestAirEmissionSource(_installationB.Id);
        _kgh = MeasureUnitsData.KgPerHour();
        _pollutant = PollutantsData.FirstTestPollutant(_kgh.Id);
        _deviceA = MonitoringDevicesData.FirstTestDevice(_sourceA.Id, _installationA.Id);
        _deviceB = MonitoringDevicesData.SecondTestDevice(_sourceB.Id, _installationB.Id);

        var hour = TimeSpan.FromHours(1);
        var now = DateTime.UtcNow;
        _windowEnd = new DateTime(now.Ticks - (now.Ticks % hour.Ticks), DateTimeKind.Utc);
        _windowStart = _windowEnd - hour;
    }

    [Fact]
    public async Task ShouldReturnAggregateRowPerInstallationLimit()
    {
        // Each installation has its own MassFlow limit. Each source emits within its own
        // installation, so we expect two independent rows — and the InstallationId on each
        // row points at its limit's installation.
        var (permitA, limitA) = MassFlowInstallationLimit(_installationA.Id, value: 8m);
        var (permitB, limitB) = MassFlowInstallationLimit(_installationB.Id, value: 6m);
        await Context.Set<Permit>().AddRangeAsync(permitA, permitB);
        await Context.Set<EmissionLimit>().AddRangeAsync(limitA, limitB);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMassFlow(_sourceA.Id, _deviceA.Id, kgPerHour: 4m),
            HourlyMassFlow(_sourceB.Id, _deviceB.Id, kgPerHour: 9m));
        await SaveChangesAsync();

        var points = await GetAggregatesAsync();
        points.Should().HaveCount(2);

        var a = points.Single(p => p.LimitId == limitA.Id);
        a.InstallationId.Should().Be(_installationA.Id);
        a.InstallationName.Should().Be(_installationA.Name);
        a.AggregateValue.Should().BeApproximately(4m, 0.0001m);
        a.Severity.Should().BeApproximately(0.5m, 0.0001m);
        a.SeverityLevel.Should().Be("green");
        a.ContributingSourcesCount.Should().Be(1);

        var b = points.Single(p => p.LimitId == limitB.Id);
        b.InstallationId.Should().Be(_installationB.Id);
        b.InstallationName.Should().Be(_installationB.Name);
        b.AggregateValue.Should().BeApproximately(9m, 0.0001m);
        b.Severity.Should().BeApproximately(1.5m, 0.0001m);
        b.SeverityLevel.Should().Be("exceedance");
        b.ContributingSourcesCount.Should().Be(1);
    }

    [Fact]
    public async Task ShouldNotMixSourcesAcrossInstallationsInSiteSum()
    {
        // installationA has a MassFlow limit but only sourceA contributes to it. sourceB sits
        // under installationB which has no limit — its 100 kg/h must NOT seep into A's sum.
        var (permit, limit) = MassFlowInstallationLimit(_installationA.Id, value: 10m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMassFlow(_sourceA.Id, _deviceA.Id, kgPerHour: 5m),
            HourlyMassFlow(_sourceB.Id, _deviceB.Id, kgPerHour: 100m));
        await SaveChangesAsync();

        var points = await GetAggregatesAsync();
        points.Should().HaveCount(1);
        points[0].LimitId.Should().Be(limit.Id);
        points[0].InstallationId.Should().Be(_installationA.Id);
        points[0].AggregateValue.Should().BeApproximately(5m, 0.0001m,
            "sourceB belongs to a different installation and must not enter A's aggregate");
        points[0].ContributingSourcesCount.Should().Be(1);
    }

    [Fact]
    public async Task ShouldReturnEmptyWhenSiteHasNoInstallationLevelLimits()
    {
        await SaveChangesAsync();

        var points = await GetAggregatesAsync();
        points.Should().BeEmpty();
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<List<ComplianceAggregatePointDto>> GetAggregatesAsync()
    {
        var url = $"api/v1/sites/{_site.Id}/compliance-aggregates?pollutantId={_pollutant.Id}";
        var response = await Client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ToResponseModel<List<ComplianceAggregatePointDto>>();
    }

    private (Permit Permit, EmissionLimit Limit) MassFlowInstallationLimit(
        Guid installationId, decimal value)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), value, LimitType.MassFlow, AveragingWindow.Hour1,
            permitId, _kgh.Id, _pollutant.Id,
            emissionSourceId: null, installationId: installationId,
            validFrom: DateTime.UtcNow.AddDays(-1), validTo: null);
        var permit = Permit.New(
            permitId, installationId,
            number: $"P-{Guid.NewGuid():N}", permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-10),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Test", notes: null,
            emissionLimits: [limit]);
        permit.ChangeStatus(PermitStatus.Active);
        return (permit, limit);
    }

    private Measurement HourlyMassFlow(Guid sourceId, Guid deviceId, decimal kgPerHour) =>
        Measurement.New(
            id: Guid.NewGuid(),
            windowStart: _windowStart, windowEnd: _windowEnd,
            window: AveragingWindow.Hour1, aggregation: Aggregation.Average,
            emissionSourceId: sourceId, pollutantId: _pollutant.Id,
            deviceId: deviceId, unitId: _kgh.Id,
            value: kgPerHour, validPointsCount: 60, expectedPointsCount: 60);

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddRangeAsync(_installationA, _installationB);
        await Context.Set<EmissionSource>().AddRangeAsync(_sourceA, _sourceB);
        await Context.Set<Pollutant>().AddAsync(_pollutant);
        await Context.Set<MeasureUnit>().AddAsync(_kgh);
        await Context.Set<MonitoringDevice>().AddRangeAsync(_deviceA, _deviceB);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
