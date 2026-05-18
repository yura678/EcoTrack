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

public class ComplianceDetectionServiceTests : BaseIntegrationTest, IAsyncLifetime
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
    private readonly MeasureUnit _g;
    private readonly MeasureUnit _kgh;
    private readonly MeasureUnit _m3h;
    private readonly MeasureUnit _percent;
    private readonly MonitoringDevice _device;

    private readonly DateTime _lastClosedHourStart;
    private readonly DateTime _lastClosedHourEnd;

    public ComplianceDetectionServiceTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;

        _sector = SectorsData.FirstTestSector();
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _iedCategory = IedCategoriesData.FirstTestIedCategory();
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _pollutant = PollutantsData.FirstTestPollutant();
        _mg = MeasureUnitsData.MgPerM3();
        _g = MeasureUnitsData.GPerM3();
        _kgh = MeasureUnitsData.KgPerHour();
        _m3h = MeasureUnitsData.CubicMetersPerHour();
        _percent = MeasureUnitsData.Percent();
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);
        // Backdate so default tests aren't suppressed by NewDeviceGraceDays.
        BackdateInstall(_device, TimeSpan.FromDays(60));

        var hour = TimeSpan.FromHours(1);
        var now = DateTime.UtcNow;
        _lastClosedHourEnd = new DateTime(
            now.Ticks - (now.Ticks % hour.Ticks), DateTimeKind.Utc);
        _lastClosedHourStart = _lastClosedHourEnd - hour;
    }

    // ─── LimitExceedance ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ShouldDetectExceedanceWithSameUnits()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 50m, unitId: _mg.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        var measurement = HourlyMeasurement(value: 80m, unitId: _mg.Id);
        await Context.Set<Measurement>().AddAsync(measurement);
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.6m, 0.0001m);
        events[0].MeasurementId.Should().Be(measurement.Id);
        events[0].EmissionSourceId.Should().Be(_source.Id);
    }

    [Fact]
    public async Task ShouldDetectExceedanceWithUnitConversion()
    {
        // Limit 50 mg/m3 = 50 base; Measurement 0.08 g/m3 = 80 base → ratio 1.6.
        var (permit, limit) = ActivePermitWithLimit(value: 50m, unitId: _mg.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        var measurement = HourlyMeasurement(value: 0.08m, unitId: _g.Id);
        await Context.Set<Measurement>().AddAsync(measurement);
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.6m, 0.0001m);
    }

    [Fact]
    public async Task ShouldNotDetectExceedanceWhenWithinLimit()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 50m, unitId: _mg.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 30m, unitId: _mg.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldNotDetectExceedanceWhenPermitIsDraft()
    {
        var (permit, limit) = PermitWithLimit(value: 50m, unitId: _mg.Id, status: PermitStatus.Draft);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 100m, unitId: _mg.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldNotDuplicateOpenExceedanceEvent()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 50m, unitId: _mg.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        var measurement = HourlyMeasurement(value: 80m, unitId: _mg.Id);
        await Context.Set<Measurement>().AddAsync(measurement);

        var existing = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id, measurement.Id, limit.Id, 1.5m,
            _lastClosedHourStart, _lastClosedHourEnd);
        existing.AssignTenant(_enterprise.Id);
        await Context.Set<ComplianceEvent>().AddAsync(existing);
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Id.Should().Be(existing.Id);
    }

    // ─── DeviceOffline ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ShouldDetectDeviceOfflineWhenNoRecentMeasurements()
    {
        // Device exists but raw_measurement is older than 30-min threshold.
        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            time: DateTime.UtcNow.AddHours(-2),
            emissionSourceId: _source.Id,
            pollutantId: _pollutant.Id,
            deviceId: _device.Id,
            unitId: _mg.Id,
            rawValue: 10m));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.DeviceOffline);
        events.Should().ContainSingle(e => e.DeviceId == _device.Id);
    }

    [Fact]
    public async Task ShouldNotDetectDeviceOfflineWhenRecentMeasurementsExist()
    {
        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            time: DateTime.UtcNow.AddMinutes(-1),
            emissionSourceId: _source.Id,
            pollutantId: _pollutant.Id,
            deviceId: _device.Id,
            unitId: _mg.Id,
            rawValue: 10m));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.DeviceOffline);
        events.Should().BeEmpty();
    }

    // ─── CalibrationFailure ──────────────────────────────────────────────────────

    [Fact]
    public async Task ShouldDetectCalibrationFailureWhenLatestFailed()
    {
        await Context.Set<CalibrationRecord>().AddRangeAsync(
            CalibrationRecordsData.Passing(_device.Id),
            CalibrationRecordsData.Failed(_device.Id));
        // Insert recent raw so this test doesn't also fire DeviceOffline (focuses assertion).
        await SeedRecentRawAsync();
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.CalibrationFailure);
        events.Should().ContainSingle(e => e.DeviceId == _device.Id);
    }

    [Fact]
    public async Task ShouldDetectCalibrationFailureWhenOverdue()
    {
        await Context.Set<CalibrationRecord>().AddAsync(CalibrationRecordsData.Overdue(_device.Id));
        await SeedRecentRawAsync();
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.CalibrationFailure);
        events.Should().ContainSingle(e => e.DeviceId == _device.Id);
    }

    [Fact]
    public async Task ShouldNotDetectCalibrationFailureWhenLatestPassedAndNotOverdue()
    {
        await Context.Set<CalibrationRecord>().AddAsync(CalibrationRecordsData.Passing(_device.Id));
        await SeedRecentRawAsync();
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.CalibrationFailure);
        events.Should().BeEmpty();
    }

    // ─── DataAvailabilityLoss ────────────────────────────────────────────────────

    [Fact]
    public async Task ShouldDetectDataAvailabilityLossWhenBelowThreshold()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 1000m, unitId: _mg.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        // 10 valid / 60 expected = 16.7% — well below 75% threshold.
        var measurement = HourlyMeasurement(value: 5m, unitId: _mg.Id);
        SetPointsCount(measurement, valid: 10, expected: 60);
        await Context.Set<Measurement>().AddAsync(measurement);
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.DataAvailabilityLoss);
        events.Should().ContainSingle(e => e.EmissionSourceId == _source.Id);
    }

    // ─── MissingMeasurement ──────────────────────────────────────────────────────

    [Fact]
    public async Task ShouldDetectMissingMeasurementWhenNoRawData()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 50m, unitId: _mg.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.MissingMeasurement);
        events.Should().ContainSingle(e => e.EmissionSourceId == _source.Id);
    }

    // ─── Gap fixes ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShouldDetectCalibrationFailureWhenNoRecordAndGraceExpired()
    {
        await SeedRecentRawAsync(); // suppress DeviceOffline
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.CalibrationFailure);
        events.Should().ContainSingle(e => e.DeviceId == _device.Id);
        events[0].Notes.Should().Contain("No calibration record");
    }

    [Fact]
    public async Task ShouldNotDetectCalibrationFailureForFreshlyInstalledDevice()
    {
        var freshDevice = MonitoringDevicesData.SecondTestDevice(_source.Id, _installation.Id);
        // SecondTestDevice has Status=Offline by default; make it Operational and freshly installed.
        SetDeviceStatus(freshDevice, DeviceStatus.Operational);
        BackdateInstall(freshDevice, TimeSpan.FromDays(1)); // within 7-day grace
        await Context.Set<MonitoringDevice>().AddAsync(freshDevice);
        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            time: DateTime.UtcNow.AddMinutes(-1),
            emissionSourceId: _source.Id,
            pollutantId: _pollutant.Id,
            deviceId: freshDevice.Id,
            unitId: _mg.Id,
            rawValue: 10m));
        // Suppress events for _device (already backdated 60 days): give it a calibration.
        await Context.Set<CalibrationRecord>().AddAsync(CalibrationRecordsData.Passing(_device.Id));
        await SeedRecentRawAsync();
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.CalibrationFailure);
        events.Should().NotContain(e => e.DeviceId == freshDevice.Id);
    }

    [Fact]
    public async Task ShouldNotDetectDeviceOfflineForFreshlyInstalledDevice()
    {
        var freshDevice = MonitoringDevicesData.SecondTestDevice(_source.Id, _installation.Id);
        SetDeviceStatus(freshDevice, DeviceStatus.Operational);
        BackdateInstall(freshDevice, TimeSpan.FromDays(2)); // within grace
        await Context.Set<MonitoringDevice>().AddAsync(freshDevice);
        // Give _device a recent measurement so it doesn't fire either.
        await SeedRecentRawAsync();
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.DeviceOffline);
        events.Should().NotContain(e => e.DeviceId == freshDevice.Id);
    }

    [Fact]
    public async Task ShouldNotDetectExceedanceWhenMeasurementInvalid()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 50m, unitId: _mg.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        var measurement = HourlyMeasurement(value: 100m, unitId: _mg.Id);
        measurement.MarkQuality(Quality.Invalid, "Sensor malfunction");
        await Context.Set<Measurement>().AddAsync(measurement);
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldDetectExceedanceOnSubstitutedMeasurement()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 80m, unitId: _mg.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        var measurement = HourlyMeasurement(value: 50m, unitId: _mg.Id);
        // Substitute replaces Value with 85 — exceeds limit 80.
        measurement.MarkSubstituted(SubstitutionSource.Auto, "test substitute", substituteValue: 85m);
        await Context.Set<Measurement>().AddAsync(measurement);
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.0625m, 0.0001m); // 85/80
    }

    // ─── MassFlow detection (LimitType dispatch) ─────────────────────────────────

    [Fact]
    public async Task ShouldDetectMassFlowExceedance()
    {
        var (permit, limit) = ActivePermitWithLimit(
            value: 50m, unitId: _kgh.Id, limitType: LimitType.MassFlow);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 80m, unitId: _kgh.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.6m, 0.0001m);
        events[0].Notes.Should().Contain("kg/h");
    }

    [Fact]
    public async Task ShouldNotDetectMassFlowExceedanceWhenWithinLimit()
    {
        var (permit, limit) = ActivePermitWithLimit(
            value: 50m, unitId: _kgh.Id, limitType: LimitType.MassFlow);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 30m, unitId: _kgh.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldDetectExceedanceUsingNormalizedValueWhenSet()
    {
        // Raw concentration 180 mg/m³ but normalized to 6% O₂ → 220 mg/m³.
        // Limit 200 mg/m³ → should fire because regulator's limit is on normalized basis.
        var (permit, limit) = ActivePermitWithLimit(value: 200m, unitId: _mg.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(
            HourlyMeasurement(value: 180m, unitId: _mg.Id, normalizedValue: 220m));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.1m, 0.0001m); // 220 / 200
        events[0].Notes.Should().Contain("normalized");
    }

    // ─── Installation-level aggregation ──────────────────────────────────────────

    [Fact]
    public async Task ShouldDetectInstallationAggregateExceedanceForMassFlow()
    {
        // Two sources on the same installation, each 30 kg/h → sum 60 kg/h.
        // Installation limit "≤50 kg/h" → exceedance ratio 1.2.
        var secondSource = EmissionSourcesData.SecondTestAirEmissionSource(_installation.Id);
        await Context.Set<EmissionSource>().AddAsync(secondSource);
        var secondDevice = MonitoringDevicesData.SecondTestDevice(secondSource.Id, _installation.Id);
        SetDeviceStatus(secondDevice, DeviceStatus.Operational);
        BackdateInstall(secondDevice, TimeSpan.FromDays(60));
        await Context.Set<MonitoringDevice>().AddAsync(secondDevice);

        var (permit, limit) = ActivePermitWithInstallationLimit(
            value: 50m, unitId: _kgh.Id, limitType: LimitType.MassFlow);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMeasurement(value: 30m, unitId: _kgh.Id),
            HourlyMeasurementForSource(secondSource.Id, secondDevice.Id, value: 30m, unitId: _kgh.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.2m, 0.0001m);
        events[0].Notes.Should().Contain("Installation aggregate");
        events[0].Notes.Should().Contain("2 source");
    }

    [Fact]
    public async Task ShouldDetectInstallationAggregateExceedanceWithMixedDerivedMassFlow()
    {
        // Two sources on same installation, MassFlow limit 8 kg/h.
        // Source A reports 3 kg/h directly.
        // Source B reports 100 mg/m³ + 60000 m³/h flow → derived 6 kg/h.
        // Sum = 9 kg/h > 8 → ratio 1.125.
        var secondSource = EmissionSourcesData.SecondTestAirEmissionSource(_installation.Id);
        await Context.Set<EmissionSource>().AddAsync(secondSource);
        var secondDevice = MonitoringDevicesData.SecondTestDevice(secondSource.Id, _installation.Id);
        SetDeviceStatus(secondDevice, DeviceStatus.Operational);
        BackdateInstall(secondDevice, TimeSpan.FromDays(60));
        await Context.Set<MonitoringDevice>().AddAsync(secondDevice);

        var (permit, limit) = ActivePermitWithInstallationLimit(
            value: 8m, unitId: _kgh.Id, limitType: LimitType.MassFlow);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMeasurement(value: 3m, unitId: _kgh.Id),
            HourlyMeasurementForSource(secondSource.Id, secondDevice.Id, value: 100m, unitId: _mg.Id));

        // Volumetric flow for the concentration-reporting source only.
        await Context.Set<RawProcessParameter>().AddAsync(RawProcessParameter.New(
            time: _lastClosedHourStart.AddMinutes(30),
            emissionSourceId: secondSource.Id,
            deviceId: secondDevice.Id,
            parameterType: ParameterType.VolumetricFlow,
            value: 60000m,
            unitId: _m3h.Id));

        await SaveChangesAsync();
        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.125m, 0.0001m);
        events[0].Notes.Should().Contain("Installation aggregate");
        events[0].Notes.Should().Contain("2 source");
        events[0].Notes.Should().Contain("derived from concentration");
    }

    [Fact]
    public async Task ShouldHandleSameDimensionMixedUnitsInInstallationAggregate()
    {
        // Two MassFlow sources with different units of the same dimension:
        // Source A: 30 kg/h (factor 1, already in base).
        // Source B: 30000 g/h (factor 0.001 → 30 kg/h equivalent).
        // Sum in kg/h base = 60 vs 50 kg/h limit → ratio 1.2.
        var gph = MeasureUnit.New(Guid.NewGuid(), "g/h-test", MeasureUnitDimension.MassFlow, 0.001m);
        await Context.Set<MeasureUnit>().AddAsync(gph);

        var secondSource = EmissionSourcesData.SecondTestAirEmissionSource(_installation.Id);
        await Context.Set<EmissionSource>().AddAsync(secondSource);
        var secondDevice = MonitoringDevicesData.SecondTestDevice(secondSource.Id, _installation.Id);
        SetDeviceStatus(secondDevice, DeviceStatus.Operational);
        BackdateInstall(secondDevice, TimeSpan.FromDays(60));
        await Context.Set<MonitoringDevice>().AddAsync(secondDevice);

        var (permit, limit) = ActivePermitWithInstallationLimit(
            value: 50m, unitId: _kgh.Id, limitType: LimitType.MassFlow);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMeasurement(value: 30m, unitId: _kgh.Id),
            HourlyMeasurementForSource(secondSource.Id, secondDevice.Id, value: 30000m, unitId: gph.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.2m, 0.0001m);
        events[0].Notes.Should().Contain("2 source");
    }

    [Fact]
    public async Task ShouldExcludeAggregateSourceWithDimensionThatHasNoDerivationPath()
    {
        // Source A: 6 kg/h MassFlow → +6.
        // Source B: 100 % (Dimensionless) → no path to MassFlow → excluded from sum.
        // Aggregate (1 contributing source) = 6 > 5 kg/h limit → event with "1 excluded" note.
        var secondSource = EmissionSourcesData.SecondTestAirEmissionSource(_installation.Id);
        await Context.Set<EmissionSource>().AddAsync(secondSource);
        var secondDevice = MonitoringDevicesData.SecondTestDevice(secondSource.Id, _installation.Id);
        SetDeviceStatus(secondDevice, DeviceStatus.Operational);
        BackdateInstall(secondDevice, TimeSpan.FromDays(60));
        await Context.Set<MonitoringDevice>().AddAsync(secondDevice);

        var (permit, limit) = ActivePermitWithInstallationLimit(
            value: 5m, unitId: _kgh.Id, limitType: LimitType.MassFlow);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMeasurement(value: 6m, unitId: _kgh.Id),
            HourlyMeasurementForSource(secondSource.Id, secondDevice.Id, value: 100m, unitId: _percent.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.2m, 0.0001m);
        events[0].Notes.Should().Contain("1 excluded");
        events[0].Notes.Should().Contain("1 source");
    }

    [Fact]
    public async Task ShouldNotDetectInstallationAggregateForConcentrationLimit()
    {
        // Concentration installation-level limit stays per-source (intensive — doesn't sum).
        // Each source 30 mg/m³ vs limit 50 mg/m³ → no exceedance.
        var secondSource = EmissionSourcesData.SecondTestAirEmissionSource(_installation.Id);
        await Context.Set<EmissionSource>().AddAsync(secondSource);
        var secondDevice = MonitoringDevicesData.SecondTestDevice(secondSource.Id, _installation.Id);
        SetDeviceStatus(secondDevice, DeviceStatus.Operational);
        BackdateInstall(secondDevice, TimeSpan.FromDays(60));
        await Context.Set<MonitoringDevice>().AddAsync(secondDevice);

        var (permit, limit) = ActivePermitWithInstallationLimit(
            value: 50m, unitId: _mg.Id, limitType: LimitType.Concentration);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddRangeAsync(
            HourlyMeasurement(value: 30m, unitId: _mg.Id),
            HourlyMeasurementForSource(secondSource.Id, secondDevice.Id, value: 30m, unitId: _mg.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().BeEmpty();
    }

    // ─── AnnualLoad detection ────────────────────────────────────────────────────

    [Fact]
    public async Task ShouldDetectAnnualLoadExceedanceWhenAverageRateAboveLimit()
    {
        var (permit, limit) = ActivePermitWithLimit(
            value: 50m, unitId: _kgh.Id,
            limitType: LimitType.AnnualLoad,
            period: AveragingWindow.Month1);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await SeedRollingRawAsync(ratePerHour: 80m);
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.6m, 0.0001m);
        events[0].Notes.Should().Contain("AnnualLoad");
    }

    [Fact]
    public async Task ShouldDetectAnnualLoadExceedanceWithDerivedMassFlow()
    {
        // AnnualLoad limit 5 kg/h, concentration 100 mg/m³, flow 60000 m³/h
        // → derived 6 kg/h > 5 → ratio 1.2
        var (permit, limit) = ActivePermitWithLimit(
            value: 5m, unitId: _kgh.Id,
            limitType: LimitType.AnnualLoad,
            period: AveragingWindow.Month1);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        var now = DateTime.UtcNow;
        await Context.Set<RawMeasurement>().AddRangeAsync(
            Enumerable.Range(0, 5).Select(i => RawMeasurement.New(
                time: now.AddMinutes(-i - 1),
                emissionSourceId: _source.Id,
                pollutantId: _pollutant.Id,
                deviceId: _device.Id,
                unitId: _mg.Id,
                rawValue: 100m)));
        await Context.Set<RawProcessParameter>().AddRangeAsync(
            Enumerable.Range(0, 5).Select(i => RawProcessParameter.New(
                time: now.AddMinutes(-i - 1),
                emissionSourceId: _source.Id,
                deviceId: _device.Id,
                parameterType: ParameterType.VolumetricFlow,
                value: 60000m,
                unitId: _m3h.Id)));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.2m, 0.0001m);
        events[0].Notes.Should().Contain("AnnualLoad derived");
    }

    [Fact]
    public async Task ShouldNormalizeRollingConcentrationForAnnualLoadDetection()
    {
        // Pollutant with O2 reference 6%; AnnualLoad limit 50 mg/m³, raw avg 40 mg/m³.
        // At 10% O2 actual: normalized = 40 × (21-6)/(21-10) = 40 × 15/11 ≈ 54.55 > 50.
        var pollutant = PollutantsData.WithO2Reference(6m);
        await Context.Set<Pollutant>().AddAsync(pollutant);

        var (permit, limit) = ActivePermitWithLimit(
            value: 50m, unitId: _mg.Id,
            limitType: LimitType.AnnualLoad,
            period: AveragingWindow.Month1,
            pollutantIdOverride: pollutant.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        var now = DateTime.UtcNow;
        await Context.Set<RawMeasurement>().AddRangeAsync(
            Enumerable.Range(0, 10).Select(i => RawMeasurement.New(
                time: now.AddMinutes(-i - 1),
                emissionSourceId: _source.Id,
                pollutantId: pollutant.Id,
                deviceId: _device.Id,
                unitId: _mg.Id,
                rawValue: 40m)));
        await Context.Set<RawProcessParameter>().AddRangeAsync(
            Enumerable.Range(0, 10).Select(i => RawProcessParameter.New(
                time: now.AddMinutes(-i - 1),
                emissionSourceId: _source.Id,
                deviceId: _device.Id,
                parameterType: ParameterType.O2Content,
                value: 10m,
                unitId: _percent.Id)));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.0909m, 0.001m); // 54.545/50
        events[0].Notes.Should().Contain("normalized");
    }

    [Fact]
    public async Task ShouldNotDetectAnnualLoadExceedanceWhenRateWithinLimit()
    {
        var (permit, limit) = ActivePermitWithLimit(
            value: 50m, unitId: _kgh.Id,
            limitType: LimitType.AnnualLoad,
            period: AveragingWindow.Month1);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await SeedRollingRawAsync(ratePerHour: 30m);
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().BeEmpty();
    }

    // ─── Derived mass flow (Stage 3) ─────────────────────────────────────────────

    [Fact]
    public async Task ShouldDetectExceedanceWithDerivedMassFlow()
    {
        // Limit 5 kg/h, measurement 100 mg/m³, flow 60000 m³/h.
        // Derived: 100 × 60000 / 1_000_000 = 6 kg/h → ratio 1.2
        var (permit, limit) = ActivePermitWithLimit(
            value: 5m, unitId: _kgh.Id, limitType: LimitType.MassFlow);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 100m, unitId: _mg.Id));
        await Context.Set<RawProcessParameter>().AddAsync(RawProcessParameter.New(
            time: _lastClosedHourStart.AddMinutes(30),
            emissionSourceId: _source.Id,
            deviceId: _device.Id,
            parameterType: ParameterType.VolumetricFlow,
            value: 60000m,
            unitId: _m3h.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().HaveCount(1);
        events[0].Ratio.Should().BeApproximately(1.2m, 0.0001m);
        events[0].Notes.Should().Contain("Derived mass flow");
        events[0].Notes.Should().Contain("m3/h-test");
    }

    [Fact]
    public async Task ShouldSkipDerivedMassFlowWhenFlowDataUnavailable()
    {
        var (permit, limit) = ActivePermitWithLimit(
            value: 5m, unitId: _kgh.Id, limitType: LimitType.MassFlow);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 100m, unitId: _mg.Id));
        // No volumetric flow process parameter.
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldSkipLimitWhenMeasurementDimensionDiffers()
    {
        // MassFlow limit (kg/h) but measurement is Concentration (mg/m³) — incompatible dims.
        var (permit, limit) = ActivePermitWithLimit(
            value: 50m, unitId: _kgh.Id, limitType: LimitType.MassFlow);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 100m, unitId: _mg.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldNotDetectDataAvailabilityLossWhenExpectedCountZero()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 1000m, unitId: _mg.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        var measurement = HourlyMeasurement(value: 5m, unitId: _mg.Id);
        SetPointsCount(measurement, valid: 0, expected: 0);
        await Context.Set<Measurement>().AddAsync(measurement);
        await SaveChangesAsync();

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.DataAvailabilityLoss);
        events.Should().BeEmpty();
    }

    // ─── OutOfRangeReading ───────────────────────────────────────────────────────

    [Fact]
    public async Task ShouldDetectOutOfRangeReadingWhenInvalidRatioExceedsThreshold()
    {
        // 12 of 60 (20%) raw rows marked Invalid → above 10% default threshold → event opens.
        await SeedRawWithQualityMixAsync(validCount: 48, invalidCount: 12);

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.OutOfRangeReading);
        events.Should().HaveCount(1);
        events[0].EmissionSourceId.Should().Be(_source.Id);
        events[0].DeviceId.Should().Be(_device.Id);
        events[0].Ratio.Should().BeApproximately(0.20m, 0.0001m);
        events[0].Notes.Should().Contain("12/60");
        events[0].Notes.Should().Contain(_pollutant.Id.ToString());
    }

    [Fact]
    public async Task ShouldNotDetectOutOfRangeReadingWhenBelowMinSampleCount()
    {
        // 1 of 5 (20%) — above ratio but below default MinSampleCount=10 → no event.
        await SeedRawWithQualityMixAsync(validCount: 4, invalidCount: 1);

        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.OutOfRangeReading);
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldNotDuplicateOpenOutOfRangeReadingEvent()
    {
        await SeedRawWithQualityMixAsync(validCount: 48, invalidCount: 12);

        await RunDetectionAsync();
        await RunDetectionAsync();

        var events = await GetEventsAsync(ComplianceEventType.OutOfRangeReading);
        events.Should().HaveCount(1);
    }

    private async Task SeedRawWithQualityMixAsync(int validCount, int invalidCount)
    {
        var baseTime = DateTime.UtcNow.AddMinutes(-30);
        var rows = new List<RawMeasurement>(validCount + invalidCount);
        for (var i = 0; i < validCount; i++)
        {
            rows.Add(RawMeasurement.New(
                baseTime.AddSeconds(i),
                _source.Id, _pollutant.Id, _device.Id, _mg.Id,
                rawValue: 100m, quality: Quality.Valid));
        }
        for (var i = 0; i < invalidCount; i++)
        {
            rows.Add(RawMeasurement.New(
                baseTime.AddSeconds(validCount + i),
                _source.Id, _pollutant.Id, _device.Id, _mg.Id,
                rawValue: 999m, quality: Quality.Invalid));
        }
        await Context.Set<RawMeasurement>().AddRangeAsync(rows);
        await SaveChangesAsync();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithLimit(
        decimal value, Guid unitId,
        LimitType limitType = LimitType.Concentration,
        AveragingWindow period = AveragingWindow.Hour1,
        Guid? pollutantIdOverride = null)
        => PermitWithLimit(value, unitId, PermitStatus.Active, limitType, period, pollutantIdOverride);

    private (Permit Permit, EmissionLimit Limit) PermitWithLimit(
        decimal value, Guid unitId, PermitStatus status,
        LimitType limitType = LimitType.Concentration,
        AveragingWindow period = AveragingWindow.Hour1,
        Guid? pollutantIdOverride = null)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(),
            value,
            limitType,
            period,
            permitId,
            unitId,
            pollutantIdOverride ?? _pollutant.Id,
            emissionSourceId: _source.Id,
            installationId: null,
            validFrom: DateTime.UtcNow.AddDays(-1),
            validTo: null);

        var permit = Permit.New(
            permitId,
            _installation.Id,
            number: "P-TEST",
            permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-10),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Test",
            notes: null,
            emissionLimits: [limit]);

        if (status != PermitStatus.Draft)
        {
            permit.ChangeStatus(status);
        }

        return (permit, limit);
    }

    private Measurement HourlyMeasurement(decimal value, Guid unitId, decimal? normalizedValue = null) =>
        HourlyMeasurementForSource(_source.Id, _device.Id, value, unitId, normalizedValue);

    private Measurement HourlyMeasurementForSource(
        Guid sourceId, Guid deviceId, decimal value, Guid unitId, decimal? normalizedValue = null) =>
        Measurement.New(
            id: Guid.NewGuid(),
            windowStart: _lastClosedHourStart,
            windowEnd: _lastClosedHourEnd,
            window: AveragingWindow.Hour1,
            aggregation: Aggregation.Average,
            emissionSourceId: sourceId,
            pollutantId: _pollutant.Id,
            deviceId: deviceId,
            unitId: unitId,
            value: value,
            validPointsCount: 60,
            expectedPointsCount: 60,
            normalizedValue: normalizedValue);

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithInstallationLimit(
        decimal value, Guid unitId, LimitType limitType,
        AveragingWindow period = AveragingWindow.Hour1)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), value, limitType, period,
            permitId, unitId, _pollutant.Id,
            emissionSourceId: null,
            installationId: _installation.Id,
            validFrom: DateTime.UtcNow.AddDays(-1), validTo: null);

        var permit = Permit.New(
            permitId, _installation.Id, "P-INST", PermitType.Air,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddYears(1),
            "Test", null, [limit]);
        permit.ChangeStatus(PermitStatus.Active);
        return (permit, limit);
    }

    private static void SetPointsCount(Measurement m, int valid, int expected)
    {
        // Use reflection because counts are private set — domain doesn't yet expose a setter
        // that fits this scenario (counts come from materialization). Tests need direct control.
        typeof(Measurement).GetProperty(nameof(Measurement.ValidPointsCount))!
            .SetValue(m, valid);
        typeof(Measurement).GetProperty(nameof(Measurement.ExpectedPointsCount))!
            .SetValue(m, expected);
    }

    private static void BackdateInstall(MonitoringDevice device, TimeSpan howLongAgo)
    {
        typeof(MonitoringDevice).GetProperty(nameof(MonitoringDevice.InstalledAt))!
            .SetValue(device, DateTime.UtcNow - howLongAgo);
    }

    private static void SetDeviceStatus(MonitoringDevice device, DeviceStatus status)
    {
        typeof(MonitoringDevice).GetProperty(nameof(MonitoringDevice.Status))!
            .SetValue(device, status);
    }

    private async Task SeedRecentRawAsync()
    {
        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            time: DateTime.UtcNow.AddMinutes(-1),
            emissionSourceId: _source.Id,
            pollutantId: _pollutant.Id,
            deviceId: _device.Id,
            unitId: _mg.Id,
            rawValue: 10m));
    }

    private async Task SeedRollingRawAsync(decimal ratePerHour)
    {
        var now = DateTime.UtcNow;
        var rows = Enumerable.Range(0, 10).Select(i => RawMeasurement.New(
            time: now.AddMinutes(-i),
            emissionSourceId: _source.Id,
            pollutantId: _pollutant.Id,
            deviceId: _device.Id,
            unitId: _kgh.Id,
            rawValue: ratePerHour));
        await Context.Set<RawMeasurement>().AddRangeAsync(rows);
    }

    private async Task<List<ComplianceEvent>> GetEventsAsync(
        ComplianceEventType type, Guid? limitId = null)
    {
        var q = Context.Set<ComplianceEvent>().AsNoTracking()
            .Where(e => e.EventType == type);
        if (limitId.HasValue) q = q.Where(e => e.LimitId == limitId.Value);
        return await q.ToListAsync();
    }

    private async Task RunDetectionAsync()
    {
        // Force-materialise CAs so detectors see test data inserted just before this call.
        // Production code relies on Timescale's real-time aggregation + scheduled refresh.
        await Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('measurement_1m', NULL, NULL);");
        await Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('process_parameter_1m', NULL, NULL);");

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ComplianceDetectionService>();
        await service.RunAsync(CancellationToken.None);
        await service.RunAnnualLoadAsync(CancellationToken.None);
        await service.RunCalibrationChecksAsync(CancellationToken.None);
    }

    // ─── Lifecycle ───────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<Pollutant>().AddAsync(_pollutant);
        await Context.Set<MeasureUnit>().AddRangeAsync(_mg, _g, _kgh, _m3h, _percent);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
