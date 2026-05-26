using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.EmissionSources;
using Tests.Data.Enterprises;
using Tests.Data.Monitoring;

namespace Api.Tests.Integration.Compliance;

public class CurrentViolationProbeTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private readonly Sector _sector;
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory;
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly Pollutant _pollutant;
    private readonly MeasureUnit _mg;
    private readonly MeasureUnit _kgh;
    private readonly MeasureUnit _m3h;
    private MeasureUnit _ppm = null!; // resolved in InitializeAsync — pre-seeded globally
    private readonly MonitoringDevice _device;

    private readonly DateTime _windowEnd;
    private readonly DateTime _windowStart;

    public CurrentViolationProbeTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
        _sector = SectorsData.FirstTestSector();
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _iedCategory = IedCategoriesData.FirstTestIedCategory();
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _mg = MeasureUnitsData.MgPerM3();
        _kgh = MeasureUnitsData.KgPerHour();
        _m3h = MeasureUnitsData.CubicMetersPerHour();
        _pollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);

        var hour = TimeSpan.FromHours(1);
        var now = DateTime.UtcNow;
        _windowEnd = new DateTime(now.Ticks - (now.Ticks % hour.Ticks), DateTimeKind.Utc);
        _windowStart = _windowEnd - hour;
    }

    [Fact]
    public async Task ShouldReportStillViolatingWhenLatestMeasurementStillAboveLimit()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 50m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        // Latest hourly measurement is 80 — well above the 50 limit.
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 80m));

        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id,
            measurementId: null, limit.Id, ratio: 1.6m,
            _windowStart, _windowEnd, notes: "old exceedance");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(true);
    }

    [Fact]
    public async Task ShouldReportNotViolatingWhenLatestMeasurementBackBelowLimit()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 50m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        // Latest measurement returned to 30 — comfortably below 50.
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 30m));

        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id,
            measurementId: null, limit.Id, ratio: 1.6m,
            _windowStart, _windowEnd, notes: "old exceedance");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(false);
    }

    [Fact]
    public async Task ShouldReportStillViolatingWhenDeviceHasNoRecentData()
    {
        // Device has never reported (no raw_measurement at all) → still offline.
        var ev = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), _source.Id, _device.Id,
            _windowStart, _windowEnd, notes: "offline");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(true);
    }

    [Fact]
    public async Task ShouldReportNotViolatingWhenDeviceJustSentData()
    {
        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            DateTime.UtcNow.AddMinutes(-1), _source.Id, _pollutant.Id, _device.Id, _mg.Id, 5m));

        var ev = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), _source.Id, _device.Id,
            _windowStart, _windowEnd, notes: "offline");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(false);
    }

    // ─── Path 3 (derived mass flow) ──────────────────────────────────────────────

    [Fact]
    public async Task ShouldReportStillViolatingForDerivedMassFlowAboveLimit()
    {
        // Limit 5 kg/h, snapshot 100 mg/m³, flow 60 000 m³/h → derived 6 kg/h > 5 → still violating.
        var (permit, limit) = ActivePermitWithLimit(
            value: 5m, unitId: _kgh.Id, pollutantId: _pollutant.Id, limitType: LimitType.MassFlow);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 100m));
        await Context.Set<RawProcessParameter>().AddAsync(RawProcessParameter.New(
            time: _windowStart.AddMinutes(30),
            emissionSourceId: _source.Id, deviceId: _device.Id,
            parameterType: ParameterType.VolumetricFlow,
            value: 60_000m, unitId: _m3h.Id));

        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id, measurementId: null, limit.Id, ratio: 1.2m,
            _windowStart, _windowEnd, notes: "derived");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();
        await RefreshProcessParameterCaAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(true);
    }

    [Fact]
    public async Task ShouldReportNotViolatingForDerivedMassFlowBelowLimit()
    {
        // Same setup, but concentration dropped to 60 mg/m³ → derived 3.6 kg/h < 5 → no longer violating.
        var (permit, limit) = ActivePermitWithLimit(
            value: 5m, unitId: _kgh.Id, pollutantId: _pollutant.Id, limitType: LimitType.MassFlow);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 60m));
        await Context.Set<RawProcessParameter>().AddAsync(RawProcessParameter.New(
            time: _windowStart.AddMinutes(30),
            emissionSourceId: _source.Id, deviceId: _device.Id,
            parameterType: ParameterType.VolumetricFlow,
            value: 60_000m, unitId: _m3h.Id));

        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id, measurementId: null, limit.Id, ratio: 1.2m,
            _windowStart, _windowEnd, notes: "derived");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();
        await RefreshProcessParameterCaAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(false);
    }

    [Fact]
    public async Task ShouldReportNullForDerivedMassFlowWhenFlowDataIsMissing()
    {
        // Limit 5 kg/h, snapshot 100 mg/m³, but no flow row → probe cannot derive → null.
        var (permit, limit) = ActivePermitWithLimit(
            value: 5m, unitId: _kgh.Id, pollutantId: _pollutant.Id, limitType: LimitType.MassFlow);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 100m));

        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id, measurementId: null, limit.Id, ratio: 1.2m,
            _windowStart, _windowEnd, notes: "derived");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();
        await RefreshProcessParameterCaAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(null);
    }

    // ─── Path 2 (cross-dimension via molar mass) ─────────────────────────────────

    [Fact]
    public async Task ShouldReportStillViolatingForPpmLimitWithMolarMass()
    {
        // NO₂ M = 46.0055 g/mol; 50 ppm → 50 × 46.0055 / 22.414 ≈ 102.65 mg/m³.
        // Measurement 110 mg/m³ > 102.65 → still violating.
        var pollutant = PollutantsData.SecondTestPollutant(_mg.Id, molarMass: 46.0055m);
        await Context.Set<Pollutant>().AddAsync(pollutant);

        var (permit, limit) = ActivePermitWithLimit(
            value: 50m, unitId: _ppm.Id, pollutantId: pollutant.Id, limitType: LimitType.Concentration);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 110m, pollutantId: pollutant.Id));

        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id, measurementId: null, limit.Id, ratio: 1.07m,
            _windowStart, _windowEnd, notes: "ppm exceedance");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(true);
    }

    [Fact]
    public async Task ShouldReportNotViolatingForPpmLimitWhenBelowConvertedThreshold()
    {
        var pollutant = PollutantsData.SecondTestPollutant(_mg.Id, molarMass: 46.0055m);
        await Context.Set<Pollutant>().AddAsync(pollutant);

        var (permit, limit) = ActivePermitWithLimit(
            value: 50m, unitId: _ppm.Id, pollutantId: pollutant.Id, limitType: LimitType.Concentration);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        // 90 mg/m³ < 102.65 → no longer violating.
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 90m, pollutantId: pollutant.Id));

        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id, measurementId: null, limit.Id, ratio: 1.07m,
            _windowStart, _windowEnd, notes: "ppm exceedance");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(false);
    }

    [Fact]
    public async Task ShouldReportNullForPpmLimitWhenPollutantHasNoMolarMass()
    {
        // Default pollutant has no molar mass → ppm conversion fails → no flow either → null.
        var (permit, limit) = ActivePermitWithLimit(
            value: 50m, unitId: _ppm.Id, pollutantId: _pollutant.Id, limitType: LimitType.Concentration);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 110m));

        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id, measurementId: null, limit.Id, ratio: 1.07m,
            _windowStart, _windowEnd, notes: "ppm exceedance");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(null);
    }

    // ─── Annual derived mass flow ────────────────────────────────────────────────

    [Fact]
    public async Task ShouldReportStillViolatingForAnnualDerivedMassFlow()
    {
        // AnnualLoad Month1 limit 5 kg/h; rolling concentration ~100 mg/m³ + flow 60 000 m³/h
        // → derived 6 kg/h > 5 → still violating.
        var (permit, limit) = ActivePermitWithLimit(
            value: 5m, unitId: _kgh.Id, pollutantId: _pollutant.Id,
            limitType: LimitType.AnnualLoad, period: AveragingWindow.Month1);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        var now = DateTime.UtcNow;
        await Context.Set<RawMeasurement>().AddRangeAsync(
            Enumerable.Range(0, 5).Select(i => RawMeasurement.New(
                time: now.AddMinutes(-i - 1),
                emissionSourceId: _source.Id, pollutantId: _pollutant.Id,
                deviceId: _device.Id, unitId: _mg.Id, rawValue: 100m)));
        await Context.Set<RawProcessParameter>().AddRangeAsync(
            Enumerable.Range(0, 5).Select(i => RawProcessParameter.New(
                time: now.AddMinutes(-i - 1),
                emissionSourceId: _source.Id, deviceId: _device.Id,
                parameterType: ParameterType.VolumetricFlow,
                value: 60_000m, unitId: _m3h.Id)));

        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id, measurementId: null, limit.Id, ratio: 1.2m,
            now.AddDays(-30), now, notes: "annual derived");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();
        await RefreshMeasurementCaAsync();
        await RefreshProcessParameterCaAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(true);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithLimit(decimal value) =>
        ActivePermitWithLimit(value, _mg.Id, _pollutant.Id, LimitType.Concentration);

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithLimit(
        decimal value, Guid unitId, Guid pollutantId,
        LimitType limitType, AveragingWindow period = AveragingWindow.Hour1)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), value, limitType, period,
            permitId, unitId, pollutantId,
            emissionSourceId: _source.Id, installationId: null,
            validFrom: DateTime.UtcNow.AddDays(-1), validTo: null);
        var permit = Permit.New(
            permitId, _installation.Id,
            number: "P-PROBE", permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-10),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Test", notes: null,
            emissionLimits: [limit]);
        permit.ChangeStatus(PermitStatus.Active);
        return (permit, limit);
    }

    private Measurement HourlyMeasurement(decimal value, Guid? pollutantId = null) =>
        Measurement.New(
            id: Guid.NewGuid(),
            windowStart: _windowStart, windowEnd: _windowEnd,
            window: AveragingWindow.Hour1, aggregation: Aggregation.Average,
            emissionSourceId: _source.Id, pollutantId: pollutantId ?? _pollutant.Id,
            deviceId: _device.Id, unitId: _mg.Id,
            value: value, validPointsCount: 60, expectedPointsCount: 60);

    private Task RefreshMeasurementCaAsync() =>
        Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('measurement_1m', NULL, NULL);");

    private Task RefreshProcessParameterCaAsync() =>
        Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('process_parameter_1m', NULL, NULL);");

    private async Task<IReadOnlyDictionary<Guid, bool?>> RunProbeAsync(
        IReadOnlyCollection<ComplianceEvent> events)
    {
        using var scope = _factory.Services.CreateScope();
        var probe = scope.ServiceProvider.GetRequiredService<ICurrentViolationProbe>();
        return await probe.ProbeAsync(events, CancellationToken.None);
    }

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
        await Context.Set<MeasureUnit>().AddAsync(_kgh);
        await Context.Set<MeasureUnit>().AddAsync(_m3h);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();

        // ApplicationDbContextInitializer seeds "ppm" on first app boot, but ResetTenantDataAsync
        // truncates measure_unit between tests — so after the first test, the global ppm is gone.
        // Reuse-or-insert keeps the row available for every test in the class.
        var existingPpm = await Context.Set<MeasureUnit>().FirstOrDefaultAsync(u => u.Symbol == "ppm");
        if (existingPpm is not null)
        {
            _ppm = existingPpm;
        }
        else
        {
            _ppm = MeasureUnit.New(Guid.NewGuid(), "ppm", MeasureUnitDimension.Dimensionless, 1m);
            await Context.Set<MeasureUnit>().AddAsync(_ppm);
            await SaveChangesAsync();
        }
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
