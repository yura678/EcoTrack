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

namespace Api.Tests.Integration.Monitoring;

public class ComplianceHeatmapTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly Pollutant _pollutant;
    private readonly MeasureUnit _mg;
    private readonly MonitoringDevice _device;

    private readonly DateTime _windowEnd;
    private readonly DateTime _windowStart;

    public ComplianceHeatmapTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _mg = MeasureUnitsData.MgPerM3();
        _pollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);

        var hour = TimeSpan.FromHours(1);
        var now = DateTime.UtcNow;
        _windowEnd = new DateTime(now.Ticks - (now.Ticks % hour.Ticks), DateTimeKind.Utc);
        _windowStart = _windowEnd - hour;
    }

    [Fact]
    public async Task ShouldComputeSeverityFromNormalizedMeasurementVsActiveLimit()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 200m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        // Raw 100 mg/m³ but normalized to 145 mg/m³ @ 6% O2 → severity = 145 / 200 = 0.725.
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 100m, normalizedValue: 145m));
        await SaveChangesAsync();

        var points = await GetHeatmapAsync();

        points.Should().HaveCount(1);
        var p = points[0];
        p.LimitId.Should().Be(limit.Id);
        p.LimitValue.Should().Be(200m);
        p.CurrentValue.Should().Be(145m);
        p.CurrentValueIsNormalized.Should().BeTrue();
        p.Severity.Should().BeApproximately(0.725m, 0.0001m);
        p.SeverityLevel.Should().Be("warning");
        p.OpenEventCount.Should().Be(0);
    }

    [Fact]
    public async Task ShouldReturnSeverityExceedanceWhenValueAboveLimit()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 50m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 80m));
        await SaveChangesAsync();

        var points = await GetHeatmapAsync();
        points.Should().HaveCount(1);
        points[0].Severity.Should().BeApproximately(1.6m, 0.0001m);
        points[0].SeverityLevel.Should().Be("exceedance");
    }

    [Fact]
    public async Task ShouldFlagSeverityLevelExceedanceWhenOpenEventExistsEvenIfValueLow()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 200m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 50m)); // low value
        var openEvent = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id, measurementId: null, limit.Id, ratio: 1.5m,
            _windowStart, _windowEnd, notes: "earlier exceedance still open");
        await Context.Set<ComplianceEvent>().AddAsync(openEvent);
        await SaveChangesAsync();

        var points = await GetHeatmapAsync();
        points.Should().HaveCount(1);
        points[0].OpenEventCount.Should().Be(1);
        points[0].SeverityLevel.Should().Be("exceedance",
            "an unresolved event marks the source critical regardless of the latest value");
    }

    [Fact]
    public async Task ShouldReturnSourceWithSeverityNullWhenNoActiveLimit()
    {
        await SaveChangesAsync(); // no permit/limit seeded

        var points = await GetHeatmapAsync();
        points.Should().HaveCount(1);
        points[0].LimitId.Should().BeNull();
        points[0].CurrentValue.Should().BeNull();
        points[0].Severity.Should().BeNull();
        points[0].SeverityLevel.Should().Be("unknown");
    }

    [Fact]
    public async Task ShouldApplyInstallationLevelConcentrationLimitToEverySource()
    {
        // Limit attached to the installation (not to a single source) — every source must
        // be evaluated against the same number. Seed two sources, latest Measurements at
        // different levels, and verify both rows come back with the same LimitId.
        var secondSource = EmissionSourcesData.SecondTestAirEmissionSource(_installation.Id);
        await Context.Set<EmissionSource>().AddAsync(secondSource);
        var secondDevice = MonitoringDevicesData.SecondTestDevice(secondSource.Id, _installation.Id);
        await Context.Set<MonitoringDevice>().AddAsync(secondDevice);

        var (permit, limit) = ActivePermitWithInstallationLimit(value: 200m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMeasurement(value: 100m), // first source — severity 0.5
            Measurement.New(
                id: Guid.NewGuid(),
                windowStart: _windowStart, windowEnd: _windowEnd,
                window: AveragingWindow.Hour1, aggregation: Aggregation.Average,
                emissionSourceId: secondSource.Id, pollutantId: _pollutant.Id,
                deviceId: secondDevice.Id, unitId: _mg.Id,
                value: 250m, validPointsCount: 60, expectedPointsCount: 60)); // 1.25
        await SaveChangesAsync();

        var points = await GetHeatmapAsync();
        points.Should().HaveCount(2);

        var first = points.Single(p => p.EmissionSourceId == _source.Id);
        first.LimitId.Should().Be(limit.Id);
        first.LimitValue.Should().Be(200m);
        first.Severity.Should().BeApproximately(0.5m, 0.0001m);
        first.SeverityLevel.Should().Be("green");

        var second = points.Single(p => p.EmissionSourceId == secondSource.Id);
        second.LimitId.Should().Be(limit.Id, "the installation-level limit applies to both sources");
        second.LimitValue.Should().Be(200m);
        second.Severity.Should().BeApproximately(1.25m, 0.0001m);
        second.SeverityLevel.Should().Be("exceedance");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<List<ComplianceHeatmapPointDto>> GetHeatmapAsync()
    {
        var url = $"api/v1/installations/{_installation.Id}/compliance-heatmap?pollutantId={_pollutant.Id}";
        var response = await Client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ToResponseModel<List<ComplianceHeatmapPointDto>>();
    }

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithLimit(decimal value)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), value, LimitType.Concentration, AveragingWindow.Hour1,
            permitId, _mg.Id, _pollutant.Id,
            emissionSourceId: _source.Id, installationId: null,
            validFrom: DateTime.UtcNow.AddDays(-1), validTo: null);
        var permit = Permit.New(
            permitId, _installation.Id,
            number: "P-HMAP", permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-10),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Test", notes: null,
            emissionLimits: [limit]);
        permit.ChangeStatus(PermitStatus.Active);
        return (permit, limit);
    }

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithInstallationLimit(decimal value)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), value, LimitType.Concentration, AveragingWindow.Hour1,
            permitId, _mg.Id, _pollutant.Id,
            emissionSourceId: null, installationId: _installation.Id,
            validFrom: DateTime.UtcNow.AddDays(-1), validTo: null);
        var permit = Permit.New(
            permitId, _installation.Id,
            number: "P-HMAP-INST", permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-10),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Test", notes: null,
            emissionLimits: [limit]);
        permit.ChangeStatus(PermitStatus.Active);
        return (permit, limit);
    }

    private Measurement HourlyMeasurement(decimal value, decimal? normalizedValue = null) =>
        Measurement.New(
            id: Guid.NewGuid(),
            windowStart: _windowStart, windowEnd: _windowEnd,
            window: AveragingWindow.Hour1, aggregation: Aggregation.Average,
            emissionSourceId: _source.Id, pollutantId: _pollutant.Id,
            deviceId: _device.Id, unitId: _mg.Id,
            value: value, validPointsCount: 60, expectedPointsCount: 60,
            normalizedValue: normalizedValue);

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<Pollutant>().AddAsync(_pollutant);
        await Context.Set<MeasureUnit>().AddAsync(_mg);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
