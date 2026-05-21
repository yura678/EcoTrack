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

namespace Api.Tests.Integration.Monitoring;

/// <summary>
/// Phase 5b: <see cref="IRawMeasurementQueries.GetComplianceAuditAsync"/> compares historical
/// measurements to a hypothetical limit. Both sides must converge in the pollutant's canonical
/// unit so a device-swap doesn't fake exceedances and so ppm-stated limits work for pollutants
/// with a known molar mass.
/// </summary>
public class ComplianceAuditCanonicalTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly MeasureUnit _mg;
    private readonly MeasureUnit _ug;

    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly MonitoringDevice _device;

    private readonly DateTime _windowStart;
    private readonly DateTime _windowEnd;

    public ComplianceAuditCanonicalTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;

        _mg = MeasureUnitsData.MgPerM3();
        _ug = MeasureUnitsData.UgPerM3();

        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);

        var hour = TimeSpan.FromHours(1);
        var now = DateTime.UtcNow;
        _windowEnd = new DateTime(now.Ticks - (now.Ticks % hour.Ticks), DateTimeKind.Utc);
        _windowStart = _windowEnd - hour;
    }

    [Fact]
    public async Task ShouldExceedLimitWhenMixedUnitAverageInCanonicalIsAboveLimit()
    {
        // 30 min of 100 mg/m³ + 30 min of 200000 µg/m³ (= 200 mg/m³) → bucket avg 150 mg/m³.
        // Limit 120 mg/m³ → exceedance fires (1/1 buckets exceeded, ratio 1.25).
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        await SaveChangesAsync();

        await SeedHalfHourAsync(pollutant.Id, _mg.Id, 100m, fromMinute: 0);
        await SeedHalfHourAsync(pollutant.Id, _ug.Id, 200000m, fromMinute: 30);
        await SaveChangesAsync();
        await RefreshCasAsync();

        var queries = ResolveQueries();
        var result = await queries.GetComplianceAuditAsync(new ComplianceAuditQueryParams(
            _source.Id, pollutant.Id,
            LimitValue: 120m, LimitUnitId: _mg.Id, Period: AveragingWindow.Hour1,
            From: _windowStart, To: _windowEnd), CancellationToken.None);

        result.Should().NotBeNull();
        result!.BucketsWithData.Should().Be(1);
        result.ExceedanceBuckets.Should().Be(1);
        result.MaxValueInBase.Should().BeApproximately(150m, 0.001m,
            "mixed-unit slices fold into canonical 150 mg/m³ per bucket");
        result.AvgValueInBase.Should().BeApproximately(150m, 0.001m);
        result.MaxRatio.Should().BeApproximately(1.25m, 0.001m,
            "150 / 120 = 1.25");
        result.LimitUnitSymbol.Should().Be(_mg.Symbol,
            "result reports limit in canonical unit");
    }

    [Fact]
    public async Task ShouldConvertPpmLimitViaMolarMassWhenAuditingAgainstMgReadings()
    {
        // Pollutant with M = 46 g/mol (NO₂-like). Raw is in mg/m³. Limit is 100 ppm — old code
        // returned null (dimension mismatch); new code converts 100 ppm → 100×46/22.414 ≈
        // 205.23 mg/m³. Raw average is 100 mg/m³, well below.
        var ppm = await EnsurePpmUnitAsync();
        var pollutant = MakeMolarPollutant(_mg.Id, molarMass: 46m);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        await SaveChangesAsync();

        await SeedHalfHourAsync(pollutant.Id, _mg.Id, 100m, fromMinute: 0);
        await SeedHalfHourAsync(pollutant.Id, _mg.Id, 100m, fromMinute: 30);
        await SaveChangesAsync();
        await RefreshCasAsync();

        var queries = ResolveQueries();
        var result = await queries.GetComplianceAuditAsync(new ComplianceAuditQueryParams(
            _source.Id, pollutant.Id,
            LimitValue: 100m, LimitUnitId: ppm.Id, Period: AveragingWindow.Hour1,
            From: _windowStart, To: _windowEnd), CancellationToken.None);

        result.Should().NotBeNull();
        result!.LimitValueInBase.Should().BeApproximately(205.229m, 0.01m,
            "100 ppm × 46 / 22.414 ≈ 205.23 mg/m³ canonical");
        result.MaxValueInBase.Should().BeApproximately(100m, 0.001m);
        result.ExceedanceBuckets.Should().Be(0, "100 mg/m³ < 205.23 mg/m³ → no exceedance");
        result.MaxRatio.Should().BeApproximately(100m / 205.229m, 0.001m);
    }

    [Fact]
    public async Task ShouldReturnNullWhenLimitDimensionCannotConvertToCanonical()
    {
        // Pollutant canonical = mg/m³ (MassConcentration). Limit in kg/h (MassFlow) — no
        // conversion path → result is null (audit can't compare across dimensions).
        var kgh = MeasureUnitsData.KgPerHour();
        await Context.Set<MeasureUnit>().AddAsync(kgh);
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);
        await SaveChangesAsync();

        await SeedHalfHourAsync(pollutant.Id, _mg.Id, 100m, fromMinute: 0);
        await SaveChangesAsync();
        await RefreshCasAsync();

        var queries = ResolveQueries();
        var result = await queries.GetComplianceAuditAsync(new ComplianceAuditQueryParams(
            _source.Id, pollutant.Id,
            LimitValue: 1m, LimitUnitId: kgh.Id, Period: AveragingWindow.Hour1,
            From: _windowStart, To: _windowEnd), CancellationToken.None);

        result.Should().BeNull("MassFlow limit + MassConcentration canonical has no derivation path here");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task SeedHalfHourAsync(Guid pollutantId, Guid unitId, decimal valuePerMinute, int fromMinute)
    {
        var raws = Enumerable.Range(fromMinute, 30).Select(minute =>
            RawMeasurement.New(
                _windowStart.AddMinutes(minute).AddSeconds(30),
                _source.Id, pollutantId, _device.Id, unitId, valuePerMinute));
        await Context.Set<RawMeasurement>().AddRangeAsync(raws);
    }

    private async Task<MeasureUnit> EnsurePpmUnitAsync()
    {
        var existing = await Context.Set<MeasureUnit>().FirstOrDefaultAsync(u => u.Symbol == "ppm");
        if (existing is not null) return existing;
        var ppm = MeasureUnit.New(Guid.NewGuid(), "ppm", MeasureUnitDimension.Dimensionless, 1m);
        await Context.Set<MeasureUnit>().AddAsync(ppm);
        await SaveChangesAsync();
        return ppm;
    }

    private static Pollutant MakeMolarPollutant(Guid canonicalUnitId, decimal molarMass)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return Pollutant.New(
            Guid.NewGuid(),
            code: $"AUDIT-{suffix}",
            name: $"Audit test pollutant {suffix}",
            category: PollutantCategory.Gas,
            media: PollutantMedia.Air,
            defaultDimension: MeasureUnitDimension.MassConcentration,
            canonicalUnitId: canonicalUnitId,
            molarMass: molarMass);
    }

    private async Task RefreshCasAsync()
    {
        await Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('measurement_1m', NULL, NULL);");
    }

    private IRawMeasurementQueries ResolveQueries()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IRawMeasurementQueries>();
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<MeasureUnit>().AddRangeAsync(_mg, _ug);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
