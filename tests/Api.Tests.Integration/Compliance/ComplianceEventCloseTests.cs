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

namespace Api.Tests.Integration.Compliance;

public class ComplianceEventCloseTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly Pollutant _pollutant;
    private readonly MeasureUnit _mg;
    private readonly MonitoringDevice _device;

    private const string BaseRoute = "api/v1";

    public ComplianceEventCloseTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _pollutant = PollutantsData.FirstTestPollutant();
        _mg = MeasureUnitsData.MgPerM3();
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);
    }

    [Fact]
    public async Task ShouldCloseEventWithReasonAndPersistResolutionFields()
    {
        var ev = await SeedOpenEventAsync();

        var request = new CloseComplianceEventDto(
            Reason: ResolutionReason.SensorFault,
            Note: "O2 sensor pegged at 21% — sent to lab.");

        var response = await Client.PatchAsJsonAsync(
            $"{BaseRoute}/compliance-events/{ev.Id}/close", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshed = await Context.Set<ComplianceEvent>().AsNoTracking()
            .FirstAsync(e => e.Id == ev.Id);
        refreshed.Status.Should().Be(ComplianceEventStatus.Closed);
        refreshed.ResolutionReason.Should().Be(ResolutionReason.SensorFault);
        refreshed.ResolutionNote.Should().Be("O2 sensor pegged at 21% — sent to lab.");
        refreshed.ClosedAt.Should().NotBeNull();
        refreshed.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ShouldRejectCloseWhenReasonOtherAndNoteEmpty()
    {
        var ev = await SeedOpenEventAsync();

        var request = new CloseComplianceEventDto(
            Reason: ResolutionReason.Other,
            Note: null);

        var response = await Client.PatchAsJsonAsync(
            $"{BaseRoute}/compliance-events/{ev.Id}/close", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var unchanged = await Context.Set<ComplianceEvent>().AsNoTracking()
            .FirstAsync(e => e.Id == ev.Id);
        unchanged.Status.Should().Be(ComplianceEventStatus.Open);
        unchanged.ResolutionReason.Should().BeNull();
    }

    [Fact]
    public async Task ShouldReturnConflictWhenClosingAlreadyClosedEvent()
    {
        var ev = await SeedOpenEventAsync();
        // First close via the API so it's a realistic end-to-end transition.
        var firstClose = await Client.PatchAsJsonAsync(
            $"{BaseRoute}/compliance-events/{ev.Id}/close",
            new CloseComplianceEventDto(ResolutionReason.TrueExceedance, "first close"));
        firstClose.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second close on the now-Closed event must be rejected with 409.
        var response = await Client.PatchAsJsonAsync(
            $"{BaseRoute}/compliance-events/{ev.Id}/close",
            new CloseComplianceEventDto(ResolutionReason.SensorFault, "second attempt"));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var stillClosed = await Context.Set<ComplianceEvent>().AsNoTracking()
            .FirstAsync(e => e.Id == ev.Id);
        stillClosed.ResolutionReason.Should().Be(ResolutionReason.TrueExceedance);
        stillClosed.ResolutionNote.Should().Be("first close");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<ComplianceEvent> SeedOpenEventAsync()
    {
        var ev = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), _source.Id, _device.Id,
            windowStart: DateTime.UtcNow.AddHours(-2),
            windowEnd: DateTime.UtcNow.AddHours(-1),
            notes: "Test offline");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();
        return ev;
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<Pollutant>().AddAsync(_pollutant);
        await Context.Set<MeasureUnit>().AddAsync(_mg);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
