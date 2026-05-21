using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using FluentAssertions;
using Infrastructure.Compliance;
using Infrastructure.Compliance.Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.EmissionSources;
using Tests.Data.Enterprises;
using Tests.Data.Monitoring;

namespace Api.Tests.Integration.Compliance;

/// <summary>
/// Phase C: <see cref="PerEnterpriseDetectionJob"/> is the leaf of the fan-out — Hangfire's
/// worker pool will invoke it with (enterpriseId, kind). These tests exercise it directly,
/// no Hangfire infra needed: a happy-path Fast run produces both a Measurement and a
/// LimitExceedance event, and a concurrent attempt against a held advisory lock skips
/// silently rather than racing.
/// </summary>
public class PerEnterpriseDetectionJobTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Pollutant _pollutant;
    private readonly MeasureUnit _mg;

    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly MonitoringDevice _device;
    private readonly Permit _permit;
    private readonly EmissionLimit _limit;

    private readonly DateTime _windowStart;
    private readonly DateTime _windowEnd;

    public PerEnterpriseDetectionJobTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;

        _mg = MeasureUnitsData.MgPerM3();
        _pollutant = PollutantsData.FirstTestPollutant(_mg.Id);

        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);
        BackdateInstall(_device, TimeSpan.FromDays(60));

        var permitId = Guid.NewGuid();
        _limit = EmissionLimit.New(
            Guid.NewGuid(), 50m, LimitType.Concentration, AveragingWindow.Hour1,
            permitId, _mg.Id, _pollutant.Id,
            emissionSourceId: _source.Id, installationId: null,
            validFrom: DateTime.UtcNow.AddDays(-1), validTo: null);
        _permit = Permit.New(
            permitId, _installation.Id, "P-JOB", PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-10),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Inspectorate", notes: null, emissionLimits: [_limit]);
        _permit.ChangeStatus(PermitStatus.Active);

        var hour = TimeSpan.FromHours(1);
        var now = DateTime.UtcNow;
        _windowEnd = new DateTime(now.Ticks - (now.Ticks % hour.Ticks), DateTimeKind.Utc);
        _windowStart = _windowEnd - hour;
    }

    [Fact]
    public async Task ExecuteFastShouldMaterializeAndOpenLimitExceedanceEvent()
    {
        await SeedFullHourOfRawAsync(valuePerMinute: 100m);
        await SaveChangesAsync();
        await RefreshCasAsync();

        await ExecuteJobAsync(DetectionJobKind.Fast);

        var measurement = await Context.Set<Measurement>().AsNoTracking()
            .FirstOrDefaultAsync(m => m.WindowEnd == _windowEnd && m.EmissionSourceId == _source.Id);
        measurement.Should().NotBeNull("Fast kind invokes materializer before detector");
        measurement!.Value.Should().Be(100m);
        measurement.UnitId.Should().Be(_mg.Id);

        var ev = await Context.Set<ComplianceEvent>().AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmissionSourceId == _source.Id
                                       && e.EventType == ComplianceEventType.LimitExceedance);
        ev.Should().NotBeNull("100 mg/m³ > 50 mg/m³ limit → LimitExceedance opened");
    }

    [Fact]
    public async Task ExecuteFastShouldSkipWhenAdvisoryLockAlreadyHeld()
    {
        await SeedFullHourOfRawAsync(valuePerMinute: 100m);
        await SaveChangesAsync();
        await RefreshCasAsync();

        // Hold the same per-(Fast, enterprise) lock another worker would acquire — the job
        // must observe the conflict and return without persisting anything.
        var lockProvider = _factory.Services.GetRequiredService<DetectionLockProvider>();
        var lockKey = PerEnterpriseDetectionJob.ComputeLockKey(DetectionJobKind.Fast, _enterprise.Id);
        await using var heldLock = await lockProvider.TryAcquireAsync(lockKey, CancellationToken.None);
        heldLock.Should().NotBeNull("test setup must acquire the lock first");

        await ExecuteJobAsync(DetectionJobKind.Fast);

        var measurementCount = await Context.Set<Measurement>().AsNoTracking()
            .Where(m => m.WindowEnd == _windowEnd && m.EmissionSourceId == _source.Id)
            .CountAsync();
        measurementCount.Should().Be(0, "lock conflict short-circuits the job before materialization");

        var eventCount = await Context.Set<ComplianceEvent>().AsNoTracking()
            .Where(e => e.EmissionSourceId == _source.Id)
            .CountAsync();
        eventCount.Should().Be(0, "no event may be opened while the lock is held");
    }

    [Fact]
    public void ComputeLockKeyShouldYieldDistinctKeysForDifferentKinds()
    {
        var enterpriseId = Guid.NewGuid();
        var fast = PerEnterpriseDetectionJob.ComputeLockKey(DetectionJobKind.Fast, enterpriseId);
        var annual = PerEnterpriseDetectionJob.ComputeLockKey(DetectionJobKind.AnnualLoad, enterpriseId);
        var calibration = PerEnterpriseDetectionJob.ComputeLockKey(DetectionJobKind.Calibration, enterpriseId);

        new[] { fast, annual, calibration }.Distinct().Should().HaveCount(3,
            "different kinds must never collide on the advisory lock for the same tenant");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task ExecuteJobAsync(DetectionJobKind kind)
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<PerEnterpriseDetectionJob>();
        await job.ExecuteAsync(_enterprise.Id, kind, CancellationToken.None);
    }

    private async Task SeedFullHourOfRawAsync(decimal valuePerMinute)
    {
        var raws = Enumerable.Range(0, 60).Select(minute =>
            RawMeasurement.New(
                _windowStart.AddMinutes(minute).AddSeconds(30),
                _source.Id, _pollutant.Id, _device.Id, _mg.Id, valuePerMinute));
        await Context.Set<RawMeasurement>().AddRangeAsync(raws);
    }

    private async Task RefreshCasAsync()
    {
        await Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('measurement_1m', NULL, NULL);");
    }

    private static void BackdateInstall(MonitoringDevice device, TimeSpan offset)
    {
        var prop = typeof(MonitoringDevice).GetProperty(nameof(MonitoringDevice.InstalledAt));
        prop!.SetValue(device, DateTime.UtcNow - offset);
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<MeasureUnit>().AddAsync(_mg);
        await Context.Set<Pollutant>().AddAsync(_pollutant);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await Context.Set<Permit>().AddAsync(_permit);
        await Context.Set<EmissionLimit>().AddAsync(_limit);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
