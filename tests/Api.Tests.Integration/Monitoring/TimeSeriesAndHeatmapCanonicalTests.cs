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
/// Phase 5a: <see cref="IRawMeasurementQueries.GetTimeSeriesAsync"/> and
/// <see cref="IRawMeasurementQueries.GetHeatmapAsync"/> must return values in the pollutant's
/// canonical unit even when raw_measurement (and therefore measurement_1m) holds rows in mixed
/// units from a device-swap. Mirrors the materializer test
/// <c>ShouldMergeMixedUnitsIntoCanonicalAverage</c> at the read-side.
/// </summary>
public class TimeSeriesAndHeatmapCanonicalTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Pollutant _pollutant;
    private readonly MeasureUnit _mg;
    private readonly MeasureUnit _ug;

    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly MonitoringDevice _device;

    private readonly DateTime _windowStart;
    private readonly DateTime _windowEnd;

    public TimeSeriesAndHeatmapCanonicalTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;

        _mg = MeasureUnitsData.MgPerM3();
        _ug = MeasureUnitsData.UgPerM3();
        _pollutant = PollutantsData.FirstTestPollutant(_mg.Id);

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
    public async Task GetTimeSeriesShouldFoldMixedUnitsIntoCanonicalAverage()
    {
        // 30 min of 100 mg/m³ + 30 min of 200000 µg/m³ (=200 mg/m³).
        // Weighted average over the hour = (30×100 + 30×200) / 60 = 150 mg/m³.
        await SeedHalfHourAsync(_mg.Id, 100m, fromMinute: 0);
        await SeedHalfHourAsync(_ug.Id, 200000m, fromMinute: 30);
        await SaveChangesAsync();
        await RefreshCasAsync();

        var queries = ResolveQueries();
        var points = await queries.GetTimeSeriesAsync(
            _pollutant.Id, _source.Id, _windowStart, _windowEnd,
            BucketWindow.Hour1, AggregationFunc.Average, CancellationToken.None);

        points.Should().ContainSingle();
        points[0].Value.Should().BeApproximately(150m, 0.001m,
            "mg + µg slices converted to canonical mg/m³ and weighted by sample count");
        points[0].TotalPointsCount.Should().Be(60);
        points[0].ValidPointsCount.Should().Be(60);
    }

    [Fact]
    public async Task GetTimeSeriesMaxShouldReturnLargestCanonicalSliceValue()
    {
        // µg slice converts to 200 mg/m³ — larger than the mg slice's 100. Max picks 200.
        await SeedHalfHourAsync(_mg.Id, 100m, fromMinute: 0);
        await SeedHalfHourAsync(_ug.Id, 200000m, fromMinute: 30);
        await SaveChangesAsync();
        await RefreshCasAsync();

        var queries = ResolveQueries();
        var points = await queries.GetTimeSeriesAsync(
            _pollutant.Id, _source.Id, _windowStart, _windowEnd,
            BucketWindow.Hour1, AggregationFunc.Max, CancellationToken.None);

        points.Should().ContainSingle();
        points[0].Value.Should().Be(200m, "µg slice's 200000 = 200 mg/m³ is the larger of two");
    }

    [Fact]
    public async Task GetHeatmapShouldFoldMixedUnitsPerSourceAndExposeCanonicalUnit()
    {
        await SeedHalfHourAsync(_mg.Id, 100m, fromMinute: 0);
        await SeedHalfHourAsync(_ug.Id, 200000m, fromMinute: 30);
        await SaveChangesAsync();
        await RefreshCasAsync();

        var queries = ResolveQueries();
        var points = await queries.GetHeatmapAsync(
            _pollutant.Id, _windowStart, _windowEnd,
            AggregationFunc.Average, CancellationToken.None);

        var ourPoint = points.SingleOrDefault(p => p.EmissionSourceId == _source.Id);
        ourPoint.Should().NotBeNull();
        ourPoint!.Value.Should().BeApproximately(150m, 0.001m);
        ourPoint.UnitId.Should().Be(_mg.Id, "HeatmapPoint.UnitId carries the pollutant's canonical unit");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task SeedHalfHourAsync(Guid unitId, decimal valuePerMinute, int fromMinute)
    {
        var raws = Enumerable.Range(fromMinute, 30).Select(minute =>
            RawMeasurement.New(
                _windowStart.AddMinutes(minute).AddSeconds(30),
                _source.Id, _pollutant.Id, _device.Id, unitId, valuePerMinute));
        await Context.Set<RawMeasurement>().AddRangeAsync(raws);
    }

    private async Task RefreshCasAsync()
    {
        await Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('measurement_1m', NULL, NULL);");
    }

    private IRawMeasurementQueries ResolveQueries()
    {
        // Factory-scope context has no HTTP user → BypassTenantFilter = true, so we don't trip
        // over the tenant filter built into the GetTimeSeries / GetHeatmap WHERE clauses.
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IRawMeasurementQueries>();
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<MeasureUnit>().AddRangeAsync(_mg, _ug);
        await Context.Set<Pollutant>().AddAsync(_pollutant);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
