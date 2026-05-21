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

public class SiteComplianceHeatmapTests : BaseIntegrationTest, IAsyncLifetime
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
    private readonly MeasureUnit _mg;
    private readonly MonitoringDevice _deviceA;
    private readonly MonitoringDevice _deviceB;

    private readonly DateTime _windowEnd;
    private readonly DateTime _windowStart;

    public SiteComplianceHeatmapTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installationA = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _installationB = InstallationData.SecondTestInstallation(_site.Id, _iedCategory.Id);
        _sourceA = EmissionSourcesData.FirstTestEmissionSource(_installationA.Id);
        _sourceB = EmissionSourcesData.SecondTestAirEmissionSource(_installationB.Id);
        _mg = MeasureUnitsData.MgPerM3();
        _pollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        _deviceA = MonitoringDevicesData.FirstTestDevice(_sourceA.Id, _installationA.Id);
        _deviceB = MonitoringDevicesData.SecondTestDevice(_sourceB.Id, _installationB.Id);

        var hour = TimeSpan.FromHours(1);
        var now = DateTime.UtcNow;
        _windowEnd = new DateTime(now.Ticks - (now.Ticks % hour.Ticks), DateTimeKind.Utc);
        _windowStart = _windowEnd - hour;
    }

    [Fact]
    public async Task ShouldReturnSourcesFromAllInstallationsOfSite()
    {
        var (permitA, limitA) = ActivePermitWithSourceLimit(_installationA.Id, _sourceA.Id, value: 100m);
        var (permitB, limitB) = ActivePermitWithSourceLimit(_installationB.Id, _sourceB.Id, value: 200m);
        await Context.Set<Permit>().AddRangeAsync(permitA, permitB);
        await Context.Set<EmissionLimit>().AddRangeAsync(limitA, limitB);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMeasurement(_sourceA.Id, _deviceA.Id, value: 50m),
            HourlyMeasurement(_sourceB.Id, _deviceB.Id, value: 250m));
        await SaveChangesAsync();

        var points = await GetHeatmapAsync();
        points.Should().HaveCount(2);

        var a = points.Single(p => p.EmissionSourceId == _sourceA.Id);
        a.InstallationId.Should().Be(_installationA.Id);
        a.InstallationName.Should().Be(_installationA.Name);
        a.LimitId.Should().Be(limitA.Id);
        a.Severity.Should().BeApproximately(0.5m, 0.0001m);
        a.SeverityLevel.Should().Be("green");

        var b = points.Single(p => p.EmissionSourceId == _sourceB.Id);
        b.InstallationId.Should().Be(_installationB.Id);
        b.InstallationName.Should().Be(_installationB.Name);
        b.LimitId.Should().Be(limitB.Id);
        b.Severity.Should().BeApproximately(1.25m, 0.0001m);
        b.SeverityLevel.Should().Be("exceedance");
    }

    [Fact]
    public async Task ShouldNotApplyInstallationLevelLimitAcrossInstallations()
    {
        // Limit attached to installationA — must NOT colour sourceB (which belongs to installationB).
        // sourceB therefore returns with severity=null even though it has a Measurement.
        var (permit, limit) = ActivePermitWithInstallationLimit(_installationA.Id, value: 100m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMeasurement(_sourceA.Id, _deviceA.Id, value: 80m),
            HourlyMeasurement(_sourceB.Id, _deviceB.Id, value: 300m));
        await SaveChangesAsync();

        var points = await GetHeatmapAsync();
        points.Should().HaveCount(2);

        var a = points.Single(p => p.EmissionSourceId == _sourceA.Id);
        a.LimitId.Should().Be(limit.Id, "installation-level limit applies to sources of its own installation");
        a.Severity.Should().BeApproximately(0.8m, 0.0001m);

        var b = points.Single(p => p.EmissionSourceId == _sourceB.Id);
        b.LimitId.Should().BeNull("installation A's limit must not bleed onto installation B's sources");
        b.Severity.Should().BeNull();
        b.SeverityLevel.Should().Be("unknown");
    }

    [Fact]
    public async Task ShouldReturnUnknownForSourceWithoutLimit()
    {
        await SaveChangesAsync(); // no permits / limits seeded

        var points = await GetHeatmapAsync();
        points.Should().HaveCount(2);
        points.Should().OnlyContain(p => p.SeverityLevel == "unknown");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<List<ComplianceHeatmapPointDto>> GetHeatmapAsync()
    {
        var url = $"api/v1/sites/{_site.Id}/compliance-heatmap?pollutantId={_pollutant.Id}";
        var response = await Client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ToResponseModel<List<ComplianceHeatmapPointDto>>();
    }

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithSourceLimit(
        Guid installationId, Guid sourceId, decimal value)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), value, LimitType.Concentration, AveragingWindow.Hour1,
            permitId, _mg.Id, _pollutant.Id,
            emissionSourceId: sourceId, installationId: null,
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

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithInstallationLimit(
        Guid installationId, decimal value)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), value, LimitType.Concentration, AveragingWindow.Hour1,
            permitId, _mg.Id, _pollutant.Id,
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

    private Measurement HourlyMeasurement(Guid sourceId, Guid deviceId, decimal value) =>
        Measurement.New(
            id: Guid.NewGuid(),
            windowStart: _windowStart, windowEnd: _windowEnd,
            window: AveragingWindow.Hour1, aggregation: Aggregation.Average,
            emissionSourceId: sourceId, pollutantId: _pollutant.Id,
            deviceId: deviceId, unitId: _mg.Id,
            value: value, validPointsCount: 60, expectedPointsCount: 60);

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddRangeAsync(_installationA, _installationB);
        await Context.Set<EmissionSource>().AddRangeAsync(_sourceA, _sourceB);
        await Context.Set<Pollutant>().AddAsync(_pollutant);
        await Context.Set<MeasureUnit>().AddAsync(_mg);
        await Context.Set<MonitoringDevice>().AddRangeAsync(_deviceA, _deviceB);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
