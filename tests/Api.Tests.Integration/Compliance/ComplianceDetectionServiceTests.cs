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

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithLimit(
        decimal value, Guid unitId,
        LimitType limitType = LimitType.Concentration,
        AveragingWindow period = AveragingWindow.Hour1)
        => PermitWithLimit(value, unitId, PermitStatus.Active, limitType, period);

    private (Permit Permit, EmissionLimit Limit) PermitWithLimit(
        decimal value, Guid unitId, PermitStatus status,
        LimitType limitType = LimitType.Concentration,
        AveragingWindow period = AveragingWindow.Hour1)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(),
            value,
            limitType,
            period,
            permitId,
            unitId,
            _pollutant.Id,
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
        Measurement.New(
            id: Guid.NewGuid(),
            windowStart: _lastClosedHourStart,
            windowEnd: _lastClosedHourEnd,
            window: AveragingWindow.Hour1,
            aggregation: Aggregation.Average,
            emissionSourceId: _source.Id,
            pollutantId: _pollutant.Id,
            deviceId: _device.Id,
            unitId: unitId,
            value: value,
            validPointsCount: 60,
            expectedPointsCount: 60,
            normalizedValue: normalizedValue);

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
        await Context.Set<MeasureUnit>().AddRangeAsync(_mg, _g, _kgh, _m3h);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
