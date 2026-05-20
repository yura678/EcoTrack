using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tests.Common;
using Tests.Data.EmissionSources;
using Tests.Data.Enterprises;
using Tests.Data.Monitoring;

namespace Api.Tests.Integration.Enterprises;

/// <summary>
/// End-to-end coverage of the Installation.Decommissioned cascade. Transitioning an
/// installation TO Decommissioned must take down its devices and revoke its active permits in
/// the same transaction; the reverse transition must NOT silently re-commission anything —
/// bringing infrastructure back online is an explicit per-asset operator decision.
/// </summary>
public class InstallationDecommissionCascadeTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly MonitoringDevice _operationalDevice;
    private readonly MonitoringDevice _offlineDevice;
    private readonly MonitoringDevice _alreadyDecommissionedDevice;

    private const string BaseRoute = "api/v1/installations";

    public InstallationDecommissionCascadeTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _operationalDevice = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);
        _offlineDevice = MonitoringDevicesData.SecondTestDevice(_source.Id, _installation.Id);
        _alreadyDecommissionedDevice = MonitoringDevicesData.DeviceToCreate(_source.Id, _installation.Id);
        // Force the third device to start in the terminal status so the cascade has something
        // it must leave untouched.
        _alreadyDecommissionedDevice.Decommission();
    }

    [Fact]
    public async Task ShouldCascadeDecommissionToDevicesAndPermits()
    {
        var activePermit = await SeedActivePermitAsync();
        var draftPermit = await SeedPermitAsync(PermitStatus.Draft);
        var revokedPermit = await SeedPermitAsync(PermitStatus.Revoked);

        var response = await PatchStatusAsync(InstallationStatus.Decommissioned);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var installation = await Context.Set<Installation>().AsNoTracking()
            .FirstAsync(i => i.Id == _installation.Id);
        installation.Status.Should().Be(InstallationStatus.Decommissioned);

        var devices = await Context.Set<MonitoringDevice>().AsNoTracking()
            .Where(d => d.InstallationId == _installation.Id)
            .ToDictionaryAsync(d => d.Id, d => d.Status);
        devices[_operationalDevice.Id].Should().Be(DeviceStatus.Decommissioned,
            "Operational devices follow the installation into Decommissioned");
        devices[_offlineDevice.Id].Should().Be(DeviceStatus.Decommissioned,
            "Any non-Decommissioned device — including Offline — is taken down too");
        devices[_alreadyDecommissionedDevice.Id].Should().Be(DeviceStatus.Decommissioned,
            "Already-Decommissioned devices stay where they are (idempotent)");

        var permits = await Context.Set<Permit>().AsNoTracking()
            .Where(p => p.InstallationId == _installation.Id)
            .ToDictionaryAsync(p => p.Id, p => p.PermitStatus);
        permits[activePermit.Id].Should().Be(PermitStatus.Revoked,
            "Active permit for a Decommissioned installation must terminate");
        permits[draftPermit.Id].Should().Be(PermitStatus.Draft,
            "Draft permits were never effective and shouldn't be auto-revoked");
        permits[revokedPermit.Id].Should().Be(PermitStatus.Revoked,
            "Already-Revoked permits remain Revoked (idempotent)");
    }

    [Fact]
    public async Task ShouldNotCascadeOnReCommission()
    {
        // First put the installation through the decommission cascade.
        await SeedActivePermitAsync();
        (await PatchStatusAsync(InstallationStatus.Decommissioned))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Now move back to Operating. Devices stay Decommissioned and permits stay Revoked —
        // re-commission is an explicit, per-asset choice the operator owns.
        var response = await PatchStatusAsync(InstallationStatus.Operating);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var devices = await Context.Set<MonitoringDevice>().AsNoTracking()
            .Where(d => d.InstallationId == _installation.Id)
            .ToListAsync();
        devices.Should().OnlyContain(d => d.Status == DeviceStatus.Decommissioned,
            "Devices stay Decommissioned — operator must explicitly re-commission each");

        var permits = await Context.Set<Permit>().AsNoTracking()
            .Where(p => p.InstallationId == _installation.Id)
            .ToListAsync();
        permits.Should().OnlyContain(p => p.PermitStatus == PermitStatus.Revoked,
            "Permits stay Revoked — operator must issue a new permit if needed");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> PatchStatusAsync(InstallationStatus status)
    {
        var body = new UpdateInstallationStatusDto(status);
        return await Client.PatchAsJsonAsync($"{BaseRoute}/{_installation.Id}", body);
    }

    private async Task<Permit> SeedActivePermitAsync() =>
        await SeedPermitAsync(PermitStatus.Active);

    private async Task<Permit> SeedPermitAsync(PermitStatus status)
    {
        var permit = Permit.New(
            id: Guid.NewGuid(),
            installationId: _installation.Id,
            number: $"P-{Guid.NewGuid():N}".Substring(0, 12),
            permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-10),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "EcoInspectorate",
            notes: null,
            emissionLimits: []);
        permit.ChangeStatus(status);
        await Context.Set<Permit>().AddAsync(permit);
        await SaveChangesAsync();
        return permit;
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<MonitoringDevice>().AddRangeAsync(
            _operationalDevice, _offlineDevice, _alreadyDecommissionedDevice);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
