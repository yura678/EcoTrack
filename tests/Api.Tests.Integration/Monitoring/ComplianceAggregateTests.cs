using System.Net;
using Api.Dtos;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tests.Common;
using Tests.Data.EmissionSources;
using Tests.Data.Enterprises;
using Tests.Data.Monitoring;

namespace Api.Tests.Integration.Monitoring;

public class ComplianceAggregateTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;
    private readonly EmissionSource _sourceA;
    private readonly EmissionSource _sourceB;
    private readonly Pollutant _pollutant;
    private readonly MeasureUnit _mg;
    private readonly MeasureUnit _kgh;
    private readonly MeasureUnit _m3h;
    private readonly MonitoringDevice _deviceA;
    private readonly MonitoringDevice _deviceB;

    private readonly DateTime _hourEnd;
    private readonly DateTime _hourStart;

    public ComplianceAggregateTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _sourceA = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _sourceB = EmissionSourcesData.SecondTestAirEmissionSource(_installation.Id);
        _pollutant = PollutantsData.FirstTestPollutant();
        _mg = MeasureUnitsData.MgPerM3();
        _kgh = MeasureUnitsData.KgPerHour();
        _m3h = MeasureUnitsData.CubicMetersPerHour();
        _deviceA = MonitoringDevicesData.FirstTestDevice(_sourceA.Id, _installation.Id);
        _deviceB = MonitoringDevicesData.SecondTestDevice(_sourceB.Id, _installation.Id);

        var hour = TimeSpan.FromHours(1);
        var now = DateTime.UtcNow;
        _hourEnd = new DateTime(now.Ticks - (now.Ticks % hour.Ticks), DateTimeKind.Utc);
        _hourStart = _hourEnd - hour;
    }

    [Fact]
    public async Task ShouldSumMassFlowAcrossSourcesForInstallationLimit()
    {
        // Limit: ≤ 8 kg/h total. Source A: 3 kg/h direct. Source B: 4 kg/h direct.
        // Sum = 7 → severity 0.875 → warning.
        var (permit, limit) = ActivePermitWithInstallationMassFlowLimit(value: 8m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMeasurement(_sourceA.Id, _deviceA.Id, value: 3m, unitId: _kgh.Id),
            HourlyMeasurement(_sourceB.Id, _deviceB.Id, value: 4m, unitId: _kgh.Id));
        await SaveChangesAsync();

        var points = await GetAggregatesAsync();
        points.Should().HaveCount(1);

        var p = points[0];
        p.LimitId.Should().Be(limit.Id);
        p.LimitType.Should().Be(LimitType.MassFlow);
        p.AggregateValue.Should().BeApproximately(7m, 0.0001m);
        p.Severity.Should().BeApproximately(0.875m, 0.0001m);
        p.SeverityLevel.Should().Be("warning");
        p.ContributingSourcesCount.Should().Be(2);
        p.DerivedSourcesCount.Should().Be(0);
        p.ExcludedSourcesCount.Should().Be(0);
    }

    [Fact]
    public async Task ShouldDeriveMassFlowFromConcentrationAndVolumetricFlow()
    {
        // Limit: ≤ 8 kg/h. Source A: 3 kg/h direct. Source B: 100 mg/m³ × 60000 m³/h → 6 kg/h.
        // Sum 9 → severity 1.125 → exceedance. DerivedCount = 1.
        var (permit, limit) = ActivePermitWithInstallationMassFlowLimit(value: 8m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMeasurement(_sourceA.Id, _deviceA.Id, value: 3m, unitId: _kgh.Id),
            HourlyMeasurement(_sourceB.Id, _deviceB.Id, value: 100m, unitId: _mg.Id));
        await Context.Set<RawProcessParameter>().AddAsync(RawProcessParameter.New(
            time: _hourStart.AddMinutes(30),
            emissionSourceId: _sourceB.Id,
            deviceId: _deviceB.Id,
            parameterType: ParameterType.VolumetricFlow,
            value: 60000m,
            unitId: _m3h.Id));
        await SaveChangesAsync();
        await RefreshCasAsync();

        var points = await GetAggregatesAsync();
        points.Should().HaveCount(1);

        points[0].AggregateValue.Should().BeApproximately(9m, 0.01m);
        points[0].Severity.Should().BeApproximately(1.125m, 0.01m);
        points[0].SeverityLevel.Should().Be("exceedance");
        points[0].ContributingSourcesCount.Should().Be(2);
        points[0].DerivedSourcesCount.Should().Be(1);
        points[0].ExcludedSourcesCount.Should().Be(0);
    }

    [Fact]
    public async Task ShouldExcludeSourceWhenDerivationDataMissing()
    {
        // Limit 8 kg/h. Source A: 3 kg/h. Source B: 100 mg/m³ but NO flow → excluded.
        // Sum from contributing = 3 → severity 0.375 → green. ExcludedCount = 1.
        var (permit, limit) = ActivePermitWithInstallationMassFlowLimit(value: 8m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMeasurement(_sourceA.Id, _deviceA.Id, value: 3m, unitId: _kgh.Id),
            HourlyMeasurement(_sourceB.Id, _deviceB.Id, value: 100m, unitId: _mg.Id));
        // No volumetric flow seeded for source B.
        await SaveChangesAsync();

        var points = await GetAggregatesAsync();
        points.Should().HaveCount(1);

        points[0].AggregateValue.Should().BeApproximately(3m, 0.0001m);
        points[0].ContributingSourcesCount.Should().Be(1);
        points[0].ExcludedSourcesCount.Should().Be(1);
    }

    [Fact]
    public async Task ShouldFlagSeverityLevelExceedanceWhenOpenEventForLimit()
    {
        // Sum well below limit (severity 0.375 = green by value) but an open ComplianceEvent
        // for the same LimitId should pin the dashboard at "exceedance" until operator acts.
        var (permit, limit) = ActivePermitWithInstallationMassFlowLimit(value: 8m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddAsync(
            HourlyMeasurement(_sourceA.Id, _deviceA.Id, value: 3m, unitId: _kgh.Id));

        var openEvent = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _sourceA.Id, measurementId: null, limit.Id, ratio: 1.5m,
            _hourStart, _hourEnd, notes: "earlier sum exceeded");
        await Context.Set<ComplianceEvent>().AddAsync(openEvent);
        await SaveChangesAsync();

        var points = await GetAggregatesAsync();
        points.Should().HaveCount(1);
        points[0].OpenEventCount.Should().Be(1);
        points[0].SeverityLevel.Should().Be("exceedance");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<List<ComplianceAggregatePointDto>> GetAggregatesAsync()
    {
        var url = $"api/v1/installations/{_installation.Id}/compliance-aggregates?pollutantId={_pollutant.Id}";
        var response = await Client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ToResponseModel<List<ComplianceAggregatePointDto>>();
    }

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithInstallationMassFlowLimit(decimal value)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), value, LimitType.MassFlow, AveragingWindow.Hour1,
            permitId, _kgh.Id, _pollutant.Id,
            emissionSourceId: null, installationId: _installation.Id,
            validFrom: DateTime.UtcNow.AddDays(-1), validTo: null);
        var permit = Permit.New(
            permitId, _installation.Id,
            number: "P-AGG", permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-10),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Test", notes: null,
            emissionLimits: [limit]);
        permit.ChangeStatus(PermitStatus.Active);
        return (permit, limit);
    }

    private Measurement HourlyMeasurement(Guid sourceId, Guid deviceId, decimal value, Guid unitId) =>
        Measurement.New(
            id: Guid.NewGuid(),
            windowStart: _hourStart, windowEnd: _hourEnd,
            window: AveragingWindow.Hour1, aggregation: Aggregation.Average,
            emissionSourceId: sourceId, pollutantId: _pollutant.Id,
            deviceId: deviceId, unitId: unitId,
            value: value, validPointsCount: 60, expectedPointsCount: 60);

    private async Task RefreshCasAsync()
    {
        await Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('process_parameter_1m', NULL, NULL);");
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddRangeAsync(_sourceA, _sourceB);
        await Context.Set<Pollutant>().AddAsync(_pollutant);
        await Context.Set<MeasureUnit>().AddRangeAsync(_mg, _kgh, _m3h);
        await Context.Set<MonitoringDevice>().AddRangeAsync(_deviceA, _deviceB);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
