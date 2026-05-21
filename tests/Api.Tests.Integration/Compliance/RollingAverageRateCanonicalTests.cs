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

/// <summary>
/// Phase 5c: <see cref="IComplianceDetectionQueries.GetRollingAverageRateAsync"/> must fold
/// mixed-unit slices into the pollutant's canonical unit before averaging — otherwise the
/// AnnualLoad detector would compare apples to oranges and fire false / miss real exceedances
/// for any source whose unit changed mid-year.
/// </summary>
public class RollingAverageRateCanonicalTests : BaseIntegrationTest, IAsyncLifetime
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

    public RollingAverageRateCanonicalTests(IntegrationTestWebFactory factory) : base(factory)
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
    public async Task ShouldFoldMixedUnitSlicesIntoCanonicalRollingAverage()
    {
        // 30 min at 100 mg/m³ + 30 min at 200000 µg/m³ (= 200 mg/m³). All Valid quality.
        // Canonical weighted-avg = (30×100 + 30×200) / 60 = 150 mg/m³.
        await SeedHalfHourValidAsync(_mg.Id, 100m, fromMinute: 0);
        await SeedHalfHourValidAsync(_ug.Id, 200000m, fromMinute: 30);
        await SaveChangesAsync();
        await RefreshCasAsync();

        var queries = ResolveQueries();
        var rolling = await queries.GetRollingAverageRateAsync(
            sourceIds: [_source.Id],
            pollutantIds: [_pollutant.Id],
            from: _windowStart,
            to: _windowEnd,
            ct: CancellationToken.None);

        rolling.Should().ContainKey((_source.Id, _pollutant.Id));
        var avg = rolling[(_source.Id, _pollutant.Id)];
        avg.UnitId.Should().Be(_mg.Id, "AvgRate is reported in the pollutant's canonical unit");
        avg.AvgRate.Should().BeApproximately(150m, 0.001m,
            "mg + µg slices fold into 150 mg/m³ canonical weighted-avg");
        avg.Samples.Should().Be(60, "total valid_count across both unit slices");
    }

    [Fact]
    public async Task ShouldReturnEmptyWhenNoValidSlicesConvert()
    {
        // No raw data at all → no slices → empty dictionary (nothing to roll up).
        await SaveChangesAsync();
        await RefreshCasAsync();

        var queries = ResolveQueries();
        var rolling = await queries.GetRollingAverageRateAsync(
            sourceIds: [_source.Id],
            pollutantIds: [_pollutant.Id],
            from: _windowStart,
            to: _windowEnd,
            ct: CancellationToken.None);

        rolling.Should().BeEmpty();
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task SeedHalfHourValidAsync(Guid unitId, decimal valuePerMinute, int fromMinute)
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

    private IComplianceDetectionQueries ResolveQueries()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IComplianceDetectionQueries>();
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
