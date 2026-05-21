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
/// Phase A guardrails: each "entry" query on IComplianceDetectionQueries can be scoped to a
/// single tenant via the new optional enterpriseId parameter. With two fully-seeded enterprises
/// living in the same DB, the scoped call must return only the requested tenant's rows; the
/// unscoped call must include both (plus any pre-existing demo-seed rows from
/// ApplicationDbContextInitializer — we assert superset, not equality, because the dev seed
/// runs on app startup and is visible under BypassTenantFilter). Reads go through a
/// factory-scope context (no HTTP user → BypassTenantFilter = true) so the global tenant
/// filter doesn't mask the scoping logic under test.
/// </summary>
public class ComplianceDetectionQueriesScopingTests : BaseIntegrationTest, IAsyncLifetime
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

    public ComplianceDetectionQueriesScopingTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;

        _mg = MeasureUnitsData.MgPerM3();
        _pollutant = PollutantsData.FirstTestPollutant(_mg.Id);

        _enterpriseA = EnterprisesData.FirstTestEquipment(_sector.Id);
        _siteA = SitesData.FirstTestSite(_enterpriseA.Id);
        _installationA = InstallationData.FirstTestInstallation(_siteA.Id, _iedCategory.Id);
        _sourceA = EmissionSourcesData.FirstTestEmissionSource(_installationA.Id);
        _deviceA = MonitoringDevicesData.FirstTestDevice(_sourceA.Id, _installationA.Id);
        (_permitA, _limitA) = MakeActivePermitAndLimit(
            _installationA.Id, _sourceA.Id, _pollutant.Id, _mg.Id, "P-A");

        _enterpriseB = EnterprisesData.SecondTestEquipment(_sector.Id);
        _siteB = SitesData.SecondTestSite(_enterpriseB.Id);
        _installationB = Installation.New(
            Guid.NewGuid(), "Enterprise B installation", _iedCategory.Id, _siteB.Id,
            InstallationStatus.Operating);
        _sourceB = EmissionSourcesData.SecondTestAirEmissionSource(_installationB.Id);
        // Build B's device Operational explicitly — MonitoringDevicesData.SecondTestDevice ships
        // Offline, which the GetOperationalDevicesAsync filter excludes. The scoping test needs
        // both tenants visible, so we side-step the fixture default here.
        _deviceB = MonitoringDevice.New(
            id: Guid.NewGuid(),
            emissionSourceId: _sourceB.Id,
            installationId: _installationB.Id,
            model: "CEMS-2000",
            serialNumber: $"SN-B-{Guid.NewGuid():N}"[..12],
            type: MonitoringDeviceType.CEMS,
            status: DeviceStatus.Operational,
            notes: "Enterprise B device (scoping test)");
        (_permitB, _limitB) = MakeActivePermitAndLimit(
            _installationB.Id, _sourceB.Id, _pollutant.Id, _mg.Id, "P-B");
    }

    [Fact]
    public async Task GetActiveLimitTargetsAsyncShouldScopeWhenEnterpriseIdProvided()
    {
        var queries = ResolveQueries();
        var ct = CancellationToken.None;

        var scopedToA = await queries.GetActiveLimitTargetsAsync(
            [LimitType.Concentration], ct, enterpriseId: _enterpriseA.Id);
        scopedToA.Should().ContainSingle()
            .Which.LimitId.Should().Be(_limitA.Id);

        var scopedToB = await queries.GetActiveLimitTargetsAsync(
            [LimitType.Concentration], ct, enterpriseId: _enterpriseB.Id);
        scopedToB.Should().ContainSingle()
            .Which.LimitId.Should().Be(_limitB.Id);

        var unscoped = await queries.GetActiveLimitTargetsAsync(
            [LimitType.Concentration], ct);
        unscoped.Select(t => t.LimitId).Should()
            .Contain([_limitA.Id, _limitB.Id]);
    }

    [Fact]
    public async Task GetActiveMaterializationTuplesAsyncShouldScopeWhenEnterpriseIdProvided()
    {
        var queries = ResolveQueries();
        var ct = CancellationToken.None;

        var scopedToA = await queries.GetActiveMaterializationTuplesAsync(
            [LimitType.Concentration], ct, enterpriseId: _enterpriseA.Id);
        scopedToA.Should().ContainSingle()
            .Which.SourceId.Should().Be(_sourceA.Id);

        var scopedToB = await queries.GetActiveMaterializationTuplesAsync(
            [LimitType.Concentration], ct, enterpriseId: _enterpriseB.Id);
        scopedToB.Should().ContainSingle()
            .Which.SourceId.Should().Be(_sourceB.Id);

        var unscoped = await queries.GetActiveMaterializationTuplesAsync(
            [LimitType.Concentration], ct);
        unscoped.Select(t => t.SourceId).Should()
            .Contain([_sourceA.Id, _sourceB.Id]);
    }

    [Fact]
    public async Task GetOperationalDevicesAsyncShouldScopeWhenEnterpriseIdProvided()
    {
        var queries = ResolveQueries();
        var ct = CancellationToken.None;

        var scopedToA = await queries.GetOperationalDevicesAsync(ct, enterpriseId: _enterpriseA.Id);
        scopedToA.Should().ContainSingle().Which.Id.Should().Be(_deviceA.Id);

        var scopedToB = await queries.GetOperationalDevicesAsync(ct, enterpriseId: _enterpriseB.Id);
        scopedToB.Should().ContainSingle().Which.Id.Should().Be(_deviceB.Id);

        var unscoped = await queries.GetOperationalDevicesAsync(ct);
        unscoped.Select(d => d.Id).Should().Contain([_deviceA.Id, _deviceB.Id]);
    }

    [Fact]
    public async Task GetDevicesWithLatestCalibrationAsyncShouldScopeWhenEnterpriseIdProvided()
    {
        var queries = ResolveQueries();
        var ct = CancellationToken.None;

        var scopedToA = await queries.GetDevicesWithLatestCalibrationAsync(ct, enterpriseId: _enterpriseA.Id);
        scopedToA.Should().ContainSingle().Which.DeviceId.Should().Be(_deviceA.Id);

        var scopedToB = await queries.GetDevicesWithLatestCalibrationAsync(ct, enterpriseId: _enterpriseB.Id);
        scopedToB.Should().ContainSingle().Which.DeviceId.Should().Be(_deviceB.Id);

        var unscoped = await queries.GetDevicesWithLatestCalibrationAsync(ct);
        unscoped.Select(d => d.DeviceId).Should().Contain([_deviceA.Id, _deviceB.Id]);
    }

    [Fact]
    public async Task GetOutOfRangeWindowsAsyncShouldScopeWhenEnterpriseIdProvided()
    {
        var now = DateTime.UtcNow;
        var from = now.AddMinutes(-10);
        // 4 invalid + 1 valid per source/device → ratio 0.8 > threshold 0.5 → both qualify.
        await SeedRawWindowAsync(_sourceA.Id, _deviceA.Id, _pollutant.Id, _mg.Id, from, invalid: 4, valid: 1);
        await SeedRawWindowAsync(_sourceB.Id, _deviceB.Id, _pollutant.Id, _mg.Id, from, invalid: 4, valid: 1);
        await SaveChangesAsync();

        var queries = ResolveQueries();
        var ct = CancellationToken.None;

        var scopedToA = await queries.GetOutOfRangeWindowsAsync(
            from, now.AddMinutes(1), threshold: 0.5m, minSampleCount: 5, ct, enterpriseId: _enterpriseA.Id);
        scopedToA.Should().ContainSingle().Which.SourceId.Should().Be(_sourceA.Id);

        var scopedToB = await queries.GetOutOfRangeWindowsAsync(
            from, now.AddMinutes(1), threshold: 0.5m, minSampleCount: 5, ct, enterpriseId: _enterpriseB.Id);
        scopedToB.Should().ContainSingle().Which.SourceId.Should().Be(_sourceB.Id);

        var unscoped = await queries.GetOutOfRangeWindowsAsync(
            from, now.AddMinutes(1), threshold: 0.5m, minSampleCount: 5, ct);
        unscoped.Select(w => w.SourceId).Should().Contain([_sourceA.Id, _sourceB.Id]);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private IComplianceDetectionQueries ResolveQueries()
    {
        // Factory-scope context has no HTTP user → BypassTenantFilter = true, so the global
        // tenant filter doesn't mask cross-tenant rows we deliberately seed in this test.
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IComplianceDetectionQueries>();
    }

    private static (Permit Permit, EmissionLimit Limit) MakeActivePermitAndLimit(
        Guid installationId, Guid sourceId, Guid pollutantId, Guid unitId, string number)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), 100m, LimitType.Concentration, AveragingWindow.Hour1,
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

    private async Task SeedRawWindowAsync(
        Guid sourceId, Guid deviceId, Guid pollutantId, Guid unitId,
        DateTime startUtc, int invalid, int valid)
    {
        var raws = new List<RawMeasurement>();
        for (var i = 0; i < invalid; i++)
        {
            raws.Add(RawMeasurement.New(
                startUtc.AddSeconds(i * 10),
                sourceId, pollutantId, deviceId, unitId, 999m, Quality.Invalid));
        }
        for (var i = 0; i < valid; i++)
        {
            raws.Add(RawMeasurement.New(
                startUtc.AddSeconds(60 + i * 10),
                sourceId, pollutantId, deviceId, unitId, 10m));
        }
        await Context.Set<RawMeasurement>().AddRangeAsync(raws);
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
