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
        var pollutant = PollutantsData.WithO2Reference(6m);
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
        var pollutant = PollutantsData.WithO2Reference(6m);
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
        var pollutant = PollutantsData.FirstTestPollutant(); // no DefaultO2Reference
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
        var pollutant = PollutantsData.WithO2Reference(6m);
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
        var pollutant = PollutantsData.FirstTestPollutant();
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
        var pollutant = PollutantsData.FirstTestPollutant();
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
        var pollutant = PollutantsData.WithO2Reference(6m);
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
