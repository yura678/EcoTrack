using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using FluentAssertions;
using Infrastructure.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.EmissionSources;
using Tests.Data.Enterprises;
using Tests.Data.Monitoring;

namespace Api.Tests.Integration.Compliance;

public class MeasurementMaterializationServiceTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private readonly Sector _sector;
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory;
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly MeasureUnit _mg;
    private readonly MeasureUnit _percent;
    private readonly MonitoringDevice _device;

    private readonly DateTime _windowStart;
    private readonly DateTime _windowEnd;
    private readonly DateTime _midWindow;

    public MeasurementMaterializationServiceTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;

        _sector = SectorsData.FirstTestSector();
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _iedCategory = IedCategoriesData.FirstTestIedCategory();
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _mg = MeasureUnitsData.MgPerM3();
        _percent = MeasureUnitsData.Percent();
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);

        var hour = TimeSpan.FromHours(1);
        var now = DateTime.UtcNow;
        _windowEnd = new DateTime(now.Ticks - (now.Ticks % hour.Ticks), DateTimeKind.Utc);
        _windowStart = _windowEnd - hour;
        _midWindow = _windowStart.AddMinutes(30);
    }

    [Fact]
    public async Task ShouldComputeNormalizedValueWhenO2DataPresent()
    {
        var pollutant = PollutantsData.WithO2Reference(6m, _mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        // 100 mg/m³ measurement at 10% O2 → normalized to 6% O2: 
        // 100 × (21 - 6) / (21 - 10) = 100 × 15 / 11 ≈ 136.363636
        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            _midWindow, _source.Id, pollutant.Id, _device.Id, _mg.Id, 100m));
        await Context.Set<RawProcessParameter>().AddAsync(RawProcessParameter.New(
            _midWindow, _source.Id, _device.Id, ParameterType.O2Content, 10m, _percent.Id));
        await SaveChangesAsync();
        await RefreshCasAsync();

        await RunMaterializationAsync();

        var measurement = await Context.Set<Measurement>().AsNoTracking()
            .FirstOrDefaultAsync(m => m.EmissionSourceId == _source.Id
                                      && m.PollutantId == pollutant.Id
                                      && m.WindowEnd == _windowEnd);

        measurement.Should().NotBeNull();
        measurement!.Value.Should().Be(100m);
        measurement.NormalizedValue.Should().NotBeNull();
        measurement.NormalizedValue!.Value.Should().BeApproximately(136.363636m, 0.0001m);
    }

    [Fact]
    public async Task ShouldLeaveNormalizedNullWhenNoO2DataAvailable()
    {
        var pollutant = PollutantsData.WithO2Reference(6m, _mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            _midWindow, _source.Id, pollutant.Id, _device.Id, _mg.Id, 100m));
        // No RawProcessParameter for O2.
        await SaveChangesAsync();
        await RefreshCasAsync();

        await RunMaterializationAsync();

        var measurement = await Context.Set<Measurement>().AsNoTracking()
            .FirstOrDefaultAsync(m => m.WindowEnd == _windowEnd && m.PollutantId == pollutant.Id);
        measurement.Should().NotBeNull();
        measurement!.NormalizedValue.Should().BeNull();
    }

    [Fact]
    public async Task ShouldLeaveNormalizedNullWhenPollutantHasNoO2Reference()
    {
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id); // no DefaultO2Reference
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            _midWindow, _source.Id, pollutant.Id, _device.Id, _mg.Id, 100m));
        await Context.Set<RawProcessParameter>().AddAsync(RawProcessParameter.New(
            _midWindow, _source.Id, _device.Id, ParameterType.O2Content, 10m, _percent.Id));
        await SaveChangesAsync();
        await RefreshCasAsync();

        await RunMaterializationAsync();

        var measurement = await Context.Set<Measurement>().AsNoTracking()
            .FirstOrDefaultAsync(m => m.WindowEnd == _windowEnd && m.PollutantId == pollutant.Id);
        measurement.Should().NotBeNull();
        measurement!.NormalizedValue.Should().BeNull();
    }

    [Fact]
    public async Task ShouldApplyFullNormalizationWhenAllProcessParamsPresent()
    {
        var pollutant = PollutantsData.WithO2Reference(6m, _mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        // Measured 100 mg/m³ at: O2=10%, T=200°C, P=98 kPa, H2O=5%
        // → 100 × (21-6)/(21-10) × (200+273.15)/273.15 × 101.325/98 × 1/(1-0.05) ≈ 257.07
        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            _midWindow, _source.Id, pollutant.Id, _device.Id, _mg.Id, 100m));
        await Context.Set<RawProcessParameter>().AddRangeAsync(
            RawProcessParameter.New(_midWindow, _source.Id, _device.Id,
                ParameterType.O2Content, 10m, _percent.Id),
            RawProcessParameter.New(_midWindow, _source.Id, _device.Id,
                ParameterType.StackTemperature, 200m, _percent.Id),
            RawProcessParameter.New(_midWindow, _source.Id, _device.Id,
                ParameterType.StackPressure, 98m, _percent.Id),
            RawProcessParameter.New(_midWindow, _source.Id, _device.Id,
                ParameterType.MoistureContent, 5m, _percent.Id));
        await SaveChangesAsync();
        await RefreshCasAsync();

        await RunMaterializationAsync();

        var measurement = await Context.Set<Measurement>().AsNoTracking()
            .FirstOrDefaultAsync(m => m.WindowEnd == _windowEnd && m.PollutantId == pollutant.Id);
        measurement.Should().NotBeNull();
        measurement!.NormalizedValue.Should().NotBeNull();
        measurement.NormalizedValue!.Value.Should().BeApproximately(257.07m, 0.5m);
    }

    [Fact]
    public async Task ShouldSubstituteValueWhenAvailabilityLowAndHistoryExists()
    {
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        // Historical valid hourly measurements; max = 80, so substitute = 80 × 1.05 = 84.
        await Context.Set<Measurement>().AddRangeAsync(
            HistoricalValidMeasurement(pollutant.Id, 70m, hoursAgo: 5),
            HistoricalValidMeasurement(pollutant.Id, 80m, hoursAgo: 4),
            HistoricalValidMeasurement(pollutant.Id, 60m, hoursAgo: 3));

        // Only 1 raw point in the current hour → ValidCount=1, Expected=60 → ~1.67% availability.
        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            _midWindow, _source.Id, pollutant.Id, _device.Id, _mg.Id, 30m));
        await SaveChangesAsync();
        await RefreshCasAsync();

        await RunMaterializationAsync();

        var measurement = await Context.Set<Measurement>().AsNoTracking()
            .FirstOrDefaultAsync(m => m.WindowEnd == _windowEnd && m.PollutantId == pollutant.Id);
        measurement.Should().NotBeNull();
        measurement!.Quality.Should().Be(Quality.Substituted);
        measurement.Value.Should().BeApproximately(84m, 0.0001m);
        measurement.SubstitutionReason.Should().Contain("substitute = max(80");
    }

    [Fact]
    public async Task ShouldMarkSubstitutedWithoutValueChangeWhenNoHistory()
    {
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            _midWindow, _source.Id, pollutant.Id, _device.Id, _mg.Id, 30m));
        await SaveChangesAsync();
        await RefreshCasAsync();

        await RunMaterializationAsync();

        var measurement = await Context.Set<Measurement>().AsNoTracking()
            .FirstOrDefaultAsync(m => m.WindowEnd == _windowEnd && m.PollutantId == pollutant.Id);
        measurement.Should().NotBeNull();
        measurement!.Quality.Should().Be(Quality.Substituted);
        measurement.Value.Should().Be(30m); // unchanged — fallback path
        measurement.SubstitutionReason.Should().Contain("no valid historical");
    }

    private Measurement HistoricalValidMeasurement(Guid pollutantId, decimal value, int hoursAgo)
    {
        var end = _windowEnd.AddHours(-hoursAgo);
        var start = end.AddHours(-1);
        return Measurement.New(
            id: Guid.NewGuid(),
            windowStart: start, windowEnd: end,
            window: AveragingWindow.Hour1, aggregation: Aggregation.Average,
            emissionSourceId: _source.Id, pollutantId: pollutantId,
            deviceId: _device.Id, unitId: _mg.Id,
            value: value,
            validPointsCount: 60, expectedPointsCount: 60);
    }

    [Fact]
    public async Task ShouldLeaveNormalizedNullWhenO2IsSensorFault()
    {
        var pollutant = PollutantsData.WithO2Reference(6m, _mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            _midWindow, _source.Id, pollutant.Id, _device.Id, _mg.Id, 100m));
        // 21.5% O2 — sensor reading ambient (disconnected). Detector must skip.
        await Context.Set<RawProcessParameter>().AddAsync(RawProcessParameter.New(
            _midWindow, _source.Id, _device.Id, ParameterType.O2Content, 21.5m, _percent.Id));
        await SaveChangesAsync();
        await RefreshCasAsync();

        await RunMaterializationAsync();

        var measurement = await Context.Set<Measurement>().AsNoTracking()
            .FirstOrDefaultAsync(m => m.WindowEnd == _windowEnd && m.PollutantId == pollutant.Id);
        measurement.Should().NotBeNull();
        measurement!.NormalizedValue.Should().BeNull();
    }

    [Fact]
    public async Task ShouldBackfillFromLimitValidFromBeyondDefaultHorizon()
    {
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);

        // ValidFrom = 7 days ago — well beyond the old 3-day fallback horizon.
        var permitId = Guid.NewGuid();
        var validFrom = DateTime.UtcNow.AddDays(-7);
        var limit = EmissionLimit.New(
            Guid.NewGuid(), 1000m, LimitType.Concentration, AveragingWindow.Hour1,
            permitId, _mg.Id, pollutant.Id,
            emissionSourceId: _source.Id, installationId: null,
            validFrom: validFrom, validTo: null);
        var permit = Permit.New(
            permitId, _installation.Id,
            number: "P-BACK", permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-10),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Test", notes: null,
            emissionLimits: [limit]);
        permit.ChangeStatus(PermitStatus.Active);
        await Context.Set<Permit>().AddAsync(permit);

        // Inside ValidFrom but outside the old 3-day default — new logic must materialize this.
        var fiveDaysAgo = DateTime.UtcNow.AddDays(-5);
        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            fiveDaysAgo, _source.Id, pollutant.Id, _device.Id, _mg.Id, 50m));

        // Before ValidFrom — must be skipped even though raw data exists.
        var tenDaysAgo = DateTime.UtcNow.AddDays(-10);
        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            tenDaysAgo, _source.Id, pollutant.Id, _device.Id, _mg.Id, 50m));

        await SaveChangesAsync();
        await RefreshCasAsync();

        await RunMaterializationAsync();

        var measurements = await Context.Set<Measurement>().AsNoTracking()
            .Where(m => m.EmissionSourceId == _source.Id && m.PollutantId == pollutant.Id)
            .ToListAsync();

        measurements.Should().Contain(m =>
            m.WindowStart <= fiveDaysAgo && m.WindowEnd > fiveDaysAgo,
            "raw point inside ValidFrom must produce a Measurement");
        measurements.Should().NotContain(m =>
            m.WindowStart <= tenDaysAgo && m.WindowEnd > tenDaysAgo,
            "raw point before ValidFrom must be skipped");
    }

    [Fact]
    public async Task ShouldRefreshMeasurementWhenLateRawDataLandsInExistingWindow()
    {
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        // First pass: 1 raw point at value=20 lands in the current closed hour.
        // Availability is low → Measurement gets Substituted (no history to use → unchanged value).
        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            _midWindow, _source.Id, pollutant.Id, _device.Id, _mg.Id, 20m));
        await SaveChangesAsync();
        await RefreshCasAsync();
        await RunMaterializationAsync();

        var firstPass = await Context.Set<Measurement>().AsNoTracking()
            .FirstAsync(m => m.WindowEnd == _windowEnd && m.PollutantId == pollutant.Id);
        firstPass.Value.Should().Be(20m);
        firstPass.ValidPointsCount.Should().Be(1);
        firstPass.Quality.Should().Be(Quality.Substituted);
        firstPass.UpdatedAt.Should().NotBeNull("first-pass substitution bumps UpdatedAt");
        var firstUpdatedAt = firstPass.UpdatedAt!.Value;

        // Late-arriving batch: 49 more points across the same hour bring availability above 75%.
        // Mean over 50 points = ((20 × 1) + (40 × 49)) / 50 = 39.6.
        var lateBatch = Enumerable.Range(0, 49).Select(i =>
            RawMeasurement.New(_windowStart.AddMinutes(i + 5),
                _source.Id, pollutant.Id, _device.Id, _mg.Id, 40m));
        await Context.Set<RawMeasurement>().AddRangeAsync(lateBatch);
        await SaveChangesAsync();
        await RefreshCasAsync();

        // Wait a tick so UpdatedAt advances visibly.
        await Task.Delay(50);
        await RunMaterializationAsync();

        var refreshed = await Context.Set<Measurement>().AsNoTracking()
            .FirstAsync(m => m.WindowEnd == _windowEnd && m.PollutantId == pollutant.Id);
        refreshed.Id.Should().Be(firstPass.Id, "rescan must update in place, not insert duplicate");
        refreshed.Value.Should().BeApproximately(39.6m, 0.1m);
        // ValidPointsCount is "minutes of usable data" (1m-buckets with at least one valid
        // reading), not the raw count of points. The 50 readings span minutes 5..53 (the late
        // batch covers minute 30 too, but the first pass already put a row there), so 49
        // distinct minutes have data.
        refreshed.ValidPointsCount.Should().Be(49);
        refreshed.Quality.Should().Be(Quality.Valid,
            "availability is back above threshold → substitution must be cleared");
        refreshed.SubstitutedAt.Should().BeNull();
        refreshed.SubstitutionReason.Should().BeNull();
        refreshed.UpdatedAt.Should().BeAfter(firstUpdatedAt);
    }

    [Fact]
    public async Task ShouldLeaveDeviceIdNullForMaterializedAggregates()
    {
        // Materialised rows are aggregates over (source, pollutant, window); the underlying raw
        // rows may belong to multiple devices, so naming a single one on the aggregate would be
        // a lie. The materializer writes null and per-device lineage stays in raw_measurement.
        var pollutant = PollutantsData.WithO2Reference(6m, _mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            _midWindow, _source.Id, pollutant.Id, _device.Id, _mg.Id, 50m));
        await SaveChangesAsync();
        await RefreshCasAsync();
        await RunMaterializationAsync();

        var m = await Context.Set<Measurement>().AsNoTracking()
            .FirstAsync(x => x.WindowEnd == _windowEnd && x.PollutantId == pollutant.Id);
        m.DeviceId.Should().BeNull("materialised aggregates carry no single-device attribution");
    }

    [Fact]
    public async Task ShouldTriggerIedSubstitutionWhenDistinctMinuteCoverageBelowThreshold()
    {
        // Seed 20 readings across 20 distinct minutes of a 60-minute window → coverage 20/60
        // ≈ 33%, well below the 75% IED Annex V threshold. Each minute carries 10 raw rows so
        // any code path that confuses "raw rows" with "valid minutes" would see availability
        // 200/60 ≈ 3.3 (above 1.0) and silently skip substitution — this test pins down the
        // corrected semantic.
        var pollutant = PollutantsData.SecondTestPollutant(_mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        var raws = Enumerable.Range(0, 20).SelectMany(minute =>
            Enumerable.Range(0, 10).Select(secondOffset =>
                RawMeasurement.New(
                    _windowStart.AddMinutes(minute).AddSeconds(secondOffset * 5),
                    _source.Id, pollutant.Id, _device.Id, _mg.Id, 30m)));
        await Context.Set<RawMeasurement>().AddRangeAsync(raws);
        await SaveChangesAsync();
        await RefreshCasAsync();
        await RunMaterializationAsync();

        var m = await Context.Set<Measurement>().AsNoTracking()
            .FirstAsync(x => x.WindowEnd == _windowEnd && x.PollutantId == pollutant.Id);
        m.ValidPointsCount.Should().Be(20, "20 distinct minutes carried valid readings");
        m.ExpectedPointsCount.Should().Be(60, "1h window = 60 expected minutes");
        m.Quality.Should().Be(Quality.Substituted,
            "33% coverage is below the 75% IED threshold — substitution must fire");
    }

    [Fact]
    public async Task ShouldMergeMixedUnitsIntoCanonicalAverage()
    {
        // Same source+pollutant in one hour, half the minutes in mg/m³ at 100 and half in µg/m³
        // at 200000. After Phase 2 conversion, both slices land at 100 mg/m³ and 200 mg/m³
        // respectively, weighted average 150 mg/m³. Persisted Measurement carries UnitId = mg/m³
        // (pollutant.CanonicalUnitId), proving cross-device temporal queries stay honest.
        var ug = MeasureUnitsData.UgPerM3();
        await Context.Set<MeasureUnit>().AddAsync(ug);
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        var firstHalf = Enumerable.Range(0, 30).Select(minute =>
            RawMeasurement.New(
                _windowStart.AddMinutes(minute).AddSeconds(30),
                _source.Id, pollutant.Id, _device.Id, _mg.Id, 100m));
        var secondHalf = Enumerable.Range(30, 30).Select(minute =>
            RawMeasurement.New(
                _windowStart.AddMinutes(minute).AddSeconds(30),
                _source.Id, pollutant.Id, _device.Id, ug.Id, 200000m));
        await Context.Set<RawMeasurement>().AddRangeAsync(firstHalf.Concat(secondHalf));
        await SaveChangesAsync();
        await RefreshCasAsync();
        await RunMaterializationAsync();

        var m = await Context.Set<Measurement>().AsNoTracking()
            .FirstAsync(x => x.WindowEnd == _windowEnd && x.PollutantId == pollutant.Id);
        m.UnitId.Should().Be(_mg.Id, "Measurement is persisted in the pollutant's canonical unit");
        m.Value.Should().Be(150m, "30×100 mg/m³ + 30×200000 µg/m³ (=200 mg/m³) → weighted avg 150 mg/m³");
        m.ValidPointsCount.Should().Be(60);
        m.ExpectedPointsCount.Should().Be(60);
    }

    [Fact]
    public async Task ShouldDropUnconvertiblePpmSliceAndKeepRemainingUnits()
    {
        // Pollutant has no MolarMass — ppm rows cannot be converted to mg/m³ and must be silently
        // dropped, leaving the materialized value computed from the convertible mg/m³ slice only.
        // Without this safeguard a misconfigured device shipping ppm to a non-gas pollutant would
        // poison the aggregate.
        var ppmUnit = MeasureUnit.New(Guid.NewGuid(), "ppm", MeasureUnitDimension.Dimensionless, 1m);
        await Context.Set<MeasureUnit>().AddAsync(ppmUnit);
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id); // MolarMass null by default
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        var mgRaws = Enumerable.Range(0, 45).Select(minute =>
            RawMeasurement.New(
                _windowStart.AddMinutes(minute).AddSeconds(15),
                _source.Id, pollutant.Id, _device.Id, _mg.Id, 80m));
        var ppmRaws = Enumerable.Range(45, 15).Select(minute =>
            RawMeasurement.New(
                _windowStart.AddMinutes(minute).AddSeconds(15),
                _source.Id, pollutant.Id, _device.Id, ppmUnit.Id, 999m));
        await Context.Set<RawMeasurement>().AddRangeAsync(mgRaws.Concat(ppmRaws));
        await SaveChangesAsync();
        await RefreshCasAsync();
        await RunMaterializationAsync();

        var m = await Context.Set<Measurement>().AsNoTracking()
            .FirstAsync(x => x.WindowEnd == _windowEnd && x.PollutantId == pollutant.Id);
        m.UnitId.Should().Be(_mg.Id);
        m.Value.Should().Be(80m, "ppm slice was dropped; the surviving mg/m³ readings averaged 80");
    }

    [Fact]
    public async Task ShouldConvertPpmToCanonicalMassWhenPollutantHasMolarMass()
    {
        // NO₂-shaped pollutant (M = 46 g/mol). A device shipping 100 ppm corresponds to
        // 100 × 46 / 22.414 ≈ 205.229 mg/m³ at EU STP. Materializer must convert and persist this.
        var ppmUnit = MeasureUnit.New(Guid.NewGuid(), "ppm", MeasureUnitDimension.Dimensionless, 1m);
        await Context.Set<MeasureUnit>().AddAsync(ppmUnit);
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id, molarMass: 46m);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        var (permit, limit) = ActivePermitWithLimit(pollutant.Id, 1000m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        var raws = Enumerable.Range(0, 60).Select(minute =>
            RawMeasurement.New(
                _windowStart.AddMinutes(minute).AddSeconds(20),
                _source.Id, pollutant.Id, _device.Id, ppmUnit.Id, 100m));
        await Context.Set<RawMeasurement>().AddRangeAsync(raws);
        await SaveChangesAsync();
        await RefreshCasAsync();
        await RunMaterializationAsync();

        var m = await Context.Set<Measurement>().AsNoTracking()
            .FirstAsync(x => x.WindowEnd == _windowEnd && x.PollutantId == pollutant.Id);
        m.UnitId.Should().Be(_mg.Id);
        m.Value.Should().BeApproximately(205.229m, 0.001m,
            "100 ppm × 46 / 22.414 ≈ 205.229 mg/m³ at EU STP");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithLimit(Guid pollutantId, decimal value)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), value, LimitType.Concentration, AveragingWindow.Hour1,
            permitId, _mg.Id, pollutantId,
            emissionSourceId: _source.Id, installationId: null,
            validFrom: DateTime.UtcNow.AddDays(-1), validTo: null);

        var permit = Permit.New(
            permitId, _installation.Id,
            number: "P-MAT", permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-10),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Test", notes: null,
            emissionLimits: [limit]);
        permit.ChangeStatus(PermitStatus.Active);
        return (permit, limit);
    }

    private async Task RefreshCasAsync()
    {
        await Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('measurement_1m', NULL, NULL);");
        await Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('process_parameter_1m', NULL, NULL);");
    }

    private async Task RunMaterializationAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MeasurementMaterializationService>();
        await service.RunAsync(CancellationToken.None);
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<MeasureUnit>().AddRangeAsync(_mg, _percent);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
