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
/// Covers the two activation guards that prevent ghost configurations on a Decommissioned
/// installation: you cannot bring a device back to a non-Decommissioned status, and you
/// cannot activate a permit on it. Both pass once the installation is recommissioned.
/// </summary>
public class DecommissionedInstallationGuardsTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly MonitoringDevice _device;

    public DecommissionedInstallationGuardsTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);
    }

    [Fact]
    public async Task ShouldRefuseDeviceActivationWhenInstallationDecommissioned()
    {
        await DecommissionInstallationAsync();

        var response = await Client.PutAsJsonAsync(
            $"api/v1/monitoring-devices/{_device.Id}",
            new UpdateMonitoringDeviceDto(_source.Id, DeviceStatus.Operational, Notes: null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Decommissioned");

        var device = await Context.Set<MonitoringDevice>().AsNoTracking()
            .FirstAsync(d => d.Id == _device.Id);
        device.Status.Should().Be(DeviceStatus.Decommissioned, "status must not change after refusal");
    }

    [Fact]
    public async Task ShouldAllowDeviceUpdateStayingDecommissioned()
    {
        // Just updating Notes while keeping status=Decommissioned must still pass even if the
        // parent installation is Decommissioned — the guard only blocks transitions OUT of
        // Decommissioned, not metadata edits on an already-terminal device.
        await DecommissionInstallationAsync();

        var response = await Client.PutAsJsonAsync(
            $"api/v1/monitoring-devices/{_device.Id}",
            new UpdateMonitoringDeviceDto(_source.Id, DeviceStatus.Decommissioned, Notes: "Sent to lab"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var device = await Context.Set<MonitoringDevice>().AsNoTracking()
            .FirstAsync(d => d.Id == _device.Id);
        device.Notes.Should().Be("Sent to lab");
    }

    [Fact]
    public async Task ShouldAllowDeviceActivationOnceInstallationRecommissioned()
    {
        await DecommissionInstallationAsync();
        await PatchInstallationStatusAsync(InstallationStatus.Operating);

        var response = await Client.PutAsJsonAsync(
            $"api/v1/monitoring-devices/{_device.Id}",
            new UpdateMonitoringDeviceDto(_source.Id, DeviceStatus.Operational, Notes: null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var device = await Context.Set<MonitoringDevice>().AsNoTracking()
            .FirstAsync(d => d.Id == _device.Id);
        device.Status.Should().Be(DeviceStatus.Operational);
    }

    [Fact]
    public async Task ShouldRefusePermitActivationWhenInstallationDecommissioned()
    {
        await DecommissionInstallationAsync();
        var draftPermit = await SeedDraftPermitAsync();

        var response = await Client.PatchAsync(
            $"api/v1/permits/{draftPermit.Id}/activate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Decommissioned");

        var permit = await Context.Set<Permit>().AsNoTracking()
            .FirstAsync(p => p.Id == draftPermit.Id);
        permit.PermitStatus.Should().Be(PermitStatus.Draft, "status must not change after refusal");
    }

    [Fact]
    public async Task ShouldAllowPermitActivationOnceInstallationRecommissioned()
    {
        await DecommissionInstallationAsync();
        var draftPermit = await SeedDraftPermitAsync();
        await PatchInstallationStatusAsync(InstallationStatus.Operating);

        var response = await Client.PatchAsync(
            $"api/v1/permits/{draftPermit.Id}/activate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var permit = await Context.Set<Permit>().AsNoTracking()
            .FirstAsync(p => p.Id == draftPermit.Id);
        permit.PermitStatus.Should().Be(PermitStatus.Active);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task DecommissionInstallationAsync() =>
        await PatchInstallationStatusAsync(InstallationStatus.Decommissioned);

    private async Task PatchInstallationStatusAsync(InstallationStatus status)
    {
        var response = await Client.PatchAsJsonAsync(
            $"api/v1/installations/{_installation.Id}",
            new UpdateInstallationStatusDto(status));
        response.IsSuccessStatusCode.Should().BeTrue(
            await response.Content.ReadAsStringAsync());
    }

    private async Task<Permit> SeedDraftPermitAsync()
    {
        var permit = Permit.New(
            id: Guid.NewGuid(),
            installationId: _installation.Id,
            number: $"P-{Guid.NewGuid():N}".Substring(0, 12),
            permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-1),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "EcoInspectorate",
            notes: null,
            emissionLimits: []);
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
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
