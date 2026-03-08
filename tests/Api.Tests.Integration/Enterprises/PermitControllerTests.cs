using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using Application.Common.Models;
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

public class PermitControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;

    private readonly MeasureUnit _mg;
    private readonly MeasureUnit _g;
    private readonly MeasureUnit _ug;

    private readonly EmissionSource _source1;
    private readonly Pollutant _pollutant1;

    private readonly Permit _draftPermit;
    private readonly EmissionLimit[] _draftLimits;

    private readonly Permit _activePermit;
    private readonly EmissionLimit[] _activeLimits;

    private readonly Permit _archivedPermit;
    private readonly EmissionLimit[] _archivedLimits;

    private const string BaseRoute = "api/v1";

    public PermitControllerTests(IntegrationTestWebFactory factory)
        : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);

        _source1 = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _pollutant1 = PollutantsData.FirstTestPollutant();

        _mg = MeasureUnitsData.MgPerM3();
        _g = MeasureUnitsData.GPerM3();
        _ug = MeasureUnitsData.UgPerM3();

        var draft = PermitsBundlesData.DraftBundle(_installation.Id, _source1.Id, _pollutant1.Id, _mg.Id);
        _draftPermit = draft.Permit;
        _draftLimits = draft.Limits;

        var active = PermitsBundlesData.ActiveBundle(_installation.Id, _source1.Id, _pollutant1.Id, _g.Id);
        _activePermit = active.Permit;
        _activeLimits = active.Limits;

        var archived = PermitsBundlesData.ArchivedBundle(_installation.Id, _source1.Id, _pollutant1.Id, _ug.Id);
        _archivedPermit = archived.Permit;
        _archivedLimits = archived.Limits;
    }


    [Fact]
    public async Task ShouldGetPagedPermits()
    {
        var url = $"{BaseRoute}/installations/{_installation.Id}/permits?page=1&pageSize=10";

        var response = await Client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.ToResponseModel<PageResult<PermitDto>>();

        page.Items.Should().Contain(i => i.Id == _draftPermit.Id);
        page.Items.Should().Contain(i => i.Id == _activePermit.Id);
        page.Items.Should().Contain(i => i.Id == _archivedPermit.Id);
    }

    [Fact]
    public async Task ShouldGetPermitById()
    {
        var response = await Client.GetAsync($"{BaseRoute}/permits/{_activePermit.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ToResponseModel<PermitDto>();
        dto.Number.Should().Be(_activePermit.Number);
        dto.EmissionLimits.Should().HaveCount(_activeLimits.Length);
    }

    [Fact]
    public async Task ShouldReturnNotFound_WhenPermitDoesNotExist()
    {
        var id = Guid.NewGuid();

        var response = await Client.GetAsync($"{BaseRoute}/permits/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShouldCreatePermit()
    {
        var limit = _draftLimits.First();

        var request = new CreatePermitDto(
            Number: "TEST-NEW",
            PermitType: PermitType.Air,
            Authority: "Authority X",
            Notes: "test-note",
            IssuedAt: DateTime.UtcNow.AddDays(-3),
            ValidUntil: DateTime.UtcNow.AddYears(2),
            EmissionLimits:
            [
                new CreateEmissionLimitDto(
                    Value: 50,
                    Period: AveragingWindow.TwentyFourHours,
                    PollutantId: limit.PollutantId,
                    EmissionSourceId: limit.EmissionSourceId,
                    UnitId: limit.UnitId,
                    ValidFrom: DateTime.UtcNow.AddDays(-3),
                    ValidTo: DateTime.UtcNow.AddYears(1))
            ]
        );

        var response = await Client.PostAsJsonAsync(
            $"{BaseRoute}/installations/{_installation.Id}/permits",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ToResponseModel<PermitDto>();

        var db = await Context.Set<Permit>()
            .Include(p => p.EmissionLimits)
            .AsNoTracking()
            .FirstAsync(p => p.Id == dto.Id);

        db.Number.Should().Be("TEST-NEW");
        db.PermitType.Should().Be(PermitType.Air);
        db.Authority.Should().Be("Authority X");

        db.EmissionLimits.Should().HaveCount(1);

        var dbLimit = db.EmissionLimits.First();
        dbLimit.Value.Should().Be(50);
        dbLimit.Period.Should().Be(AveragingWindow.TwentyFourHours);
        dbLimit.PollutantId.Should().Be(limit.PollutantId);
        dbLimit.EmissionSourceId.Should().Be(limit.EmissionSourceId);
        dbLimit.UnitId.Should().Be(limit.UnitId);

        dbLimit.ValidFrom.Should().BeCloseTo(request.EmissionLimits![0].ValidFrom!.Value, TimeSpan.FromSeconds(1));
        dbLimit.ValidTo.Should().BeCloseTo(request.EmissionLimits![0].ValidTo!.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ShouldNotCreatePermit_WhenValidationFails()
    {
        var request = new CreatePermitDto(
            Number: "",
            PermitType: PermitType.Air,
            Authority: "",
            Notes: null,
            IssuedAt: DateTime.UtcNow,
            ValidUntil: DateTime.UtcNow.AddDays(-1),
            EmissionLimits: []
        );

        var response = await Client.PostAsJsonAsync(
            $"{BaseRoute}/installations/{_installation.Id}/permits",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ShouldUpdatePermit()
    {
        var limit = _draftLimits.First();

        var request = new UpdatePermitDto(
            Number: "UPDATED",
            PermitType: PermitType.Water,
            Authority: "Updated Auth",
            Notes: "Updated Notes",
            IssuedAt: DateTime.UtcNow.AddYears(-2),
            ValidUntil: DateTime.UtcNow.AddYears(2),
            EmissionLimits:
            [
                new UpdateEmissionLimitDto(
                    Id: limit.Id,
                    Value: 999,
                    Period: AveragingWindow.OneHour,
                    PollutantId: limit.PollutantId,
                    EmissionSourceId: limit.EmissionSourceId,
                    UnitId: limit.UnitId,
                    ValidFrom: limit.ValidFrom,
                    ValidTo: limit.ValidTo
                )
            ]
        );

        var response = await Client.PutAsJsonAsync(
            $"{BaseRoute}/permits/{_draftPermit.Id}",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ToResponseModel<PermitDto>();
        var db = await Context.Set<Permit>()
            .Include(x => x.EmissionLimits)
            .FirstAsync(x => x.Id == _draftPermit.Id);

        db.Number.Should().Be("UPDATED");
        db.EmissionLimits.First().Value.Should().Be(999);
    }

    [Fact]
    public async Task ShouldNotUpdatePermit_WhenValidationFails()
    {
        var request = new UpdatePermitDto(
            Number: "",
            PermitType: PermitType.Air,
            Authority: "",
            Notes: "",
            IssuedAt: DateTime.UtcNow,
            ValidUntil: DateTime.UtcNow.AddDays(-5),
            EmissionLimits: []
        );

        var response = await Client.PutAsJsonAsync(
            $"{BaseRoute}/permits/{_draftPermit.Id}",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ShouldArchivePermit()
    {
        var response = await Client.PatchAsync(
            $"{BaseRoute}/permits/{_activePermit.Id}/archive", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var db = await Context.Set<Permit>().AsNoTracking()
            .FirstAsync(x => x.Id.Equals(_activePermit.Id));

        db.PermitStatus.Should().Be(PermitStatus.Archived);
    }

    [Fact]
    public async Task ShouldNotActivatePermit()
    {
        var response = await Client.PatchAsync(
            $"{BaseRoute}/permits/{_draftPermit.Id}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var db = await Context.Set<Permit>().AsNoTracking()
            .FirstAsync(x => x.Id.Equals(_draftPermit.Id));

        db.PermitStatus.Should().Be(PermitStatus.Draft);
    }

    [Fact]
    public async Task ShouldRevokePermit()
    {
        var response = await Client.PatchAsync(
            $"{BaseRoute}/permits/{_activePermit.Id}/revoke", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var db = await Context.Set<Permit>().AsNoTracking()
            .FirstAsync(x => x.Id.Equals(_activePermit.Id));

        db.PermitStatus.Should().Be(PermitStatus.Revoked);
    }

    [Fact]
    public async Task ShouldDeletePermit()
    {
        var response = await Client.DeleteAsync(
            $"{BaseRoute}/permits/{_draftPermit.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Context.Set<Permit>().AnyAsync(x => x.Id.Equals(_draftPermit.Id)))
            .Should().BeFalse();
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);

        await Context.Set<EmissionSource>().AddAsync(_source1);
        await Context.Set<Pollutant>().AddAsync(_pollutant1);
        await Context.Set<MeasureUnit>().AddRangeAsync(_mg, _g, _ug);
        await Context.Set<Permit>().AddRangeAsync(_draftPermit, _activePermit, _archivedPermit);
        await Context.Set<EmissionLimit>().AddRangeAsync(
            _draftLimits.Concat(_activeLimits).Concat(_archivedLimits));

        await SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Context.Set<EmissionLimit>().RemoveRange(Context.Set<EmissionLimit>());
        Context.Set<Permit>().RemoveRange(Context.Set<Permit>());
        Context.Set<Pollutant>().RemoveRange(Context.Set<Pollutant>());
        Context.Set<IedCategory>().RemoveRange(Context.Set<IedCategory>());
        Context.Set<MeasureUnit>().RemoveRange(Context.Set<MeasureUnit>());
        Context.Set<EmissionSource>().RemoveRange(Context.Set<EmissionSource>());
        Context.Set<MonitoringDevice>().RemoveRange(Context.Set<MonitoringDevice>());
        Context.Set<Installation>().RemoveRange(Context.Set<Installation>());
        Context.Set<Site>().RemoveRange(Context.Set<Site>());
        Context.Set<Enterprise>().RemoveRange(Context.Set<Enterprise>());
        Context.Set<Sector>().RemoveRange(Context.Set<Sector>());

        await SaveChangesAsync();
    }
}