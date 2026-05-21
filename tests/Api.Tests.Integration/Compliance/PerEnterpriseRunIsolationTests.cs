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

/// <summary>
/// Phase B end-to-end isolation: RunForEnterpriseAsync on both
/// <see cref="MeasurementMaterializationService"/> and <see cref="ComplianceDetectionService"/>
/// must produce Measurement / ComplianceEvent rows for one tenant only, leaving the other tenant
/// untouched. Proves the scoping path Phase A wired through entry queries actually reaches the
/// rows that get persisted.
/// </summary>
public class PerEnterpriseRunIsolationTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Pollutant _pollutant;
    private readonly MeasureUnit _mg;

    private readonly Enterprise _enterpriseA;
    private readonly Site _siteA;
    private readonly Installation _installationA;
    private readonly EmissionSource _sourceA;
    private readonly MonitoringDevice _deviceA;
    private readonly Permit _permitA;
    private readonly EmissionLimit _limitA;

    private readonly Enterprise _enterpriseB;
    private readonly Site _siteB;
    private readonly Installation _installationB;
    private readonly EmissionSource _sourceB;
    private readonly MonitoringDevice _deviceB;
    private readonly Permit _permitB;
    private readonly EmissionLimit _limitB;

    private readonly DateTime _windowStart;
    private readonly DateTime _windowEnd;

    public PerEnterpriseRunIsolationTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;

        _mg = MeasureUnitsData.MgPerM3();
        _pollutant = PollutantsData.FirstTestPollutant(_mg.Id);

        _enterpriseA = EnterprisesData.FirstTestEquipment(_sector.Id);
        _siteA = SitesData.FirstTestSite(_enterpriseA.Id);
        _installationA = InstallationData.FirstTestInstallation(_siteA.Id, _iedCategory.Id);
        _sourceA = EmissionSourcesData.FirstTestEmissionSource(_installationA.Id);
        _deviceA = MonitoringDevicesData.FirstTestDevice(_sourceA.Id, _installationA.Id);
        BackdateInstall(_deviceA, TimeSpan.FromDays(60));
        (_permitA, _limitA) = MakeActivePermitWithLimit(
            _installationA.Id, _sourceA.Id, _pollutant.Id, _mg.Id, "P-A", limitValue: 50m);

        _enterpriseB = EnterprisesData.SecondTestEquipment(_sector.Id);
        _siteB = SitesData.SecondTestSite(_enterpriseB.Id);
        _installationB = Installation.New(
            Guid.NewGuid(), "Enterprise B installation", _iedCategory.Id, _siteB.Id,
            InstallationStatus.Operating);
        _sourceB = EmissionSourcesData.SecondTestAirEmissionSource(_installationB.Id);
        _deviceB = MonitoringDevice.New(
            id: Guid.NewGuid(),
            emissionSourceId: _sourceB.Id,
            installationId: _installationB.Id,
            model: "CEMS-2000",
            serialNumber: $"SN-B-{Guid.NewGuid():N}"[..12],
            type: MonitoringDeviceType.CEMS,
            status: DeviceStatus.Operational,
            notes: "Enterprise B device");
        BackdateInstall(_deviceB, TimeSpan.FromDays(60));
        (_permitB, _limitB) = MakeActivePermitWithLimit(
            _installationB.Id, _sourceB.Id, _pollutant.Id, _mg.Id, "P-B", limitValue: 50m);

        var hour = TimeSpan.FromHours(1);
        var now = DateTime.UtcNow;
        _windowEnd = new DateTime(now.Ticks - (now.Ticks % hour.Ticks), DateTimeKind.Utc);
        _windowStart = _windowEnd - hour;
    }

    [Fact]
    public async Task MaterializeForEnterpriseShouldPersistOnlyThatTenantsMeasurement()
    {
        await SeedRawHourAsync(_sourceA, _deviceA, valuePerMinute: 100m);
        await SeedRawHourAsync(_sourceB, _deviceB, valuePerMinute: 100m);
        await SaveChangesAsync();
        await RefreshCasAsync();

        await RunMaterializerForAsync(_enterpriseA.Id);

        var aRows = await Context.Set<Measurement>().AsNoTracking()
            .Where(m => m.WindowEnd == _windowEnd && m.EmissionSourceId == _sourceA.Id)
            .CountAsync();
        var bRows = await Context.Set<Measurement>().AsNoTracking()
            .Where(m => m.WindowEnd == _windowEnd && m.EmissionSourceId == _sourceB.Id)
            .CountAsync();
        aRows.Should().Be(1, "A's Measurement is materialized by RunForEnterpriseAsync(A)");
        bRows.Should().Be(0, "B's Measurement must stay untouched");

        await RunMaterializerForAsync(_enterpriseB.Id);

        bRows = await Context.Set<Measurement>().AsNoTracking()
            .Where(m => m.WindowEnd == _windowEnd && m.EmissionSourceId == _sourceB.Id)
            .CountAsync();
        bRows.Should().Be(1, "B's Measurement appears after RunForEnterpriseAsync(B)");
    }

    [Fact]
    public async Task DetectForEnterpriseShouldOpenOnlyThatTenantsEvent()
    {
        await SeedRawHourAsync(_sourceA, _deviceA, valuePerMinute: 100m);
        await SeedRawHourAsync(_sourceB, _deviceB, valuePerMinute: 100m);
        await SaveChangesAsync();
        await RefreshCasAsync();

        // Materialise for BOTH so each tenant has a Measurement available to detect against —
        // we want this test to lock in the detector scoping, not the materializer scoping.
        await RunMaterializerForAsync(_enterpriseA.Id);
        await RunMaterializerForAsync(_enterpriseB.Id);

        await RunDetectorForAsync(_enterpriseA.Id);

        var aEvents = await Context.Set<ComplianceEvent>().AsNoTracking()
            .Where(e => e.EmissionSourceId == _sourceA.Id
                        && e.EventType == ComplianceEventType.LimitExceedance)
            .CountAsync();
        var bEvents = await Context.Set<ComplianceEvent>().AsNoTracking()
            .Where(e => e.EmissionSourceId == _sourceB.Id
                        && e.EventType == ComplianceEventType.LimitExceedance)
            .CountAsync();
        aEvents.Should().Be(1, "A's LimitExceedance opened by RunForEnterpriseAsync(A)");
        bEvents.Should().Be(0, "B's exceedance event must wait for its own per-tenant run");

        await RunDetectorForAsync(_enterpriseB.Id);

        bEvents = await Context.Set<ComplianceEvent>().AsNoTracking()
            .Where(e => e.EmissionSourceId == _sourceB.Id
                        && e.EventType == ComplianceEventType.LimitExceedance)
            .CountAsync();
        bEvents.Should().Be(1, "B's event appears after RunForEnterpriseAsync(B)");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private static (Permit Permit, EmissionLimit Limit) MakeActivePermitWithLimit(
        Guid installationId, Guid sourceId, Guid pollutantId, Guid unitId,
        string number, decimal limitValue)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), limitValue, LimitType.Concentration, AveragingWindow.Hour1,
            permitId, unitId, pollutantId,
            emissionSourceId: sourceId, installationId: null,
            validFrom: DateTime.UtcNow.AddDays(-1), validTo: null);
        var permit = Permit.New(
            permitId, installationId, number, PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-10),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Inspectorate", notes: null,
            emissionLimits: [limit]);
        permit.ChangeStatus(PermitStatus.Active);
        return (permit, limit);
    }

    private async Task SeedRawHourAsync(EmissionSource source, MonitoringDevice device, decimal valuePerMinute)
    {
        var raws = Enumerable.Range(0, 60).Select(minute =>
            RawMeasurement.New(
                _windowStart.AddMinutes(minute).AddSeconds(30),
                source.Id, _pollutant.Id, device.Id, _mg.Id, valuePerMinute));
        await Context.Set<RawMeasurement>().AddRangeAsync(raws);
    }

    private async Task RefreshCasAsync()
    {
        await Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('measurement_1m', NULL, NULL);");
    }

    private async Task RunMaterializerForAsync(Guid enterpriseId)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MeasurementMaterializationService>();
        await service.RunForEnterpriseAsync(enterpriseId, CancellationToken.None);
    }

    private async Task RunDetectorForAsync(Guid enterpriseId)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ComplianceDetectionService>();
        await service.RunForEnterpriseAsync(enterpriseId, CancellationToken.None);
    }

    private static void BackdateInstall(MonitoringDevice device, TimeSpan offset)
    {
        // Detector's NewDeviceGraceDays would otherwise suppress LimitExceedance / DeviceOffline
        // for freshly created test devices. Bypass via reflection — InstalledAt is private set.
        var prop = typeof(MonitoringDevice).GetProperty(nameof(MonitoringDevice.InstalledAt));
        prop!.SetValue(device, DateTime.UtcNow - offset);
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<MeasureUnit>().AddAsync(_mg);
        await Context.Set<Pollutant>().AddAsync(_pollutant);

        await Context.Set<Enterprise>().AddRangeAsync(_enterpriseA, _enterpriseB);
        await Context.Set<Site>().AddRangeAsync(_siteA, _siteB);
        await Context.Set<Installation>().AddRangeAsync(_installationA, _installationB);
        await Context.Set<EmissionSource>().AddRangeAsync(_sourceA, _sourceB);
        await Context.Set<MonitoringDevice>().AddRangeAsync(_deviceA, _deviceB);
        await Context.Set<Permit>().AddRangeAsync(_permitA, _permitB);
        await Context.Set<EmissionLimit>().AddRangeAsync(_limitA, _limitB);

        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
