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

namespace Api.Tests.Integration.Monitoring;

public class MonitoringPlanControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;

    private readonly EmissionSource _source1;
    private readonly EmissionSource _source2;

    private readonly Pollutant _pollutant1;
    private readonly Pollutant _pollutant2;

    private readonly MonitoringPlan _activePlan;
    private readonly MonitoringRequirement[] _activeReqs;

    private readonly MonitoringPlan _draftPlan;
    private readonly MonitoringRequirement[] _draftReqs;

    private readonly MonitoringPlan _archivedPlan;
    private readonly MonitoringRequirement[] _archivedReqs;

    private const string BaseRoute = "api/v1";

    public MonitoringPlanControllerTests(IntegrationTestWebFactory factory)
        : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);

        _source1 = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _source2 = EmissionSourcesData.SecondTestEmissionSource(_installation.Id);

        _pollutant1 = PollutantsData.FirstTestPollutant();
        _pollutant2 = PollutantsData.SecondTestPollutant();


        var active = MonitoringPlansBundlesData.ActivePlanBundle(
            _installation.Id, _source1.Id, _source2.Id, _pollutant1.Id, _pollutant2.Id);
        _activePlan = active.Plan;
        _activeReqs = active.Requirements;

        var draft = MonitoringPlansBundlesData.DraftPlanBundle(
            _installation.Id, _source1.Id, _source2.Id, _pollutant1.Id, _pollutant2.Id);
        _draftPlan = draft.Plan;
        _draftReqs = draft.Requirements;

        var archived = MonitoringPlansBundlesData.ArchivedPlanBundle(
            _installation.Id, _source1.Id, _source2.Id, _pollutant1.Id, _pollutant2.Id);
        _archivedPlan = archived.Plan;
        _archivedReqs = archived.Requirements;
    }


    [Fact]
    public async Task ShouldGetPagedMonitoringPlans()
    {
        var url = $"{BaseRoute}/installations/{_installation.Id}/monitoring-plans?page=1&pageSize=10";

        var response = await Client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.ToResponseModel<PageResult<MonitoringPlanDto>>();

        page.Items.Should().Contain(i => i.Id == _activePlan.Id);
        page.Items.Should().Contain(i => i.Id == _draftPlan.Id);
        page.Items.Should().Contain(i => i.Id == _archivedPlan.Id);
    }


    [Fact]
    public async Task ShouldGetMonitoringPlanById()
    {
        var response = await Client.GetAsync($"{BaseRoute}/monitoring-plans/{_activePlan.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ToResponseModel<MonitoringPlanDto>();
        dto.Id.Should().Be(_activePlan.Id);
        dto.MonitoringRequirements.Should().HaveCount(2);
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenPlanDoesNotExist()
    {
        var id = Guid.NewGuid();

        var response = await Client.GetAsync($"{BaseRoute}/monitoring-plans/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }


    [Fact]
    public async Task ShouldCreateMonitoringPlan()
    {
        var req = _activeReqs.First();

        var request = new CreateMonitoringPlanDto(
            Version: "v-created",
            Notes: "created-test",
            MonitoringRequirements:
            [
                new CreateMonitoringRequirementDto(
                    EmissionSourceId: req.EmissionSourceId,
                    PollutantId: req.PollutantId,
                    Frequency: Frequency.Daily
                )
            ]
        );

        var url = $"{BaseRoute}/installations/{_installation.Id}/monitoring-plans";

        var response = await Client.PostAsJsonAsync(url, request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ToResponseModel<MonitoringPlanDto>();
        dto.Version.Should().Be("v-created");
        dto.MonitoringRequirements.Should().HaveCount(1);
    }


    [Fact]
    public async Task ShouldUpdateMonitoringPlan()
    {
        var req = _draftReqs.First();

        var request = new UpdateMonitoringPlanDto(
            Version: "v-updated",
            Notes: "some-notes",
            MonitoringRequirements:
            [
                new UpdateMonitoringRequirementDto(
                    Id: req.Id,
                    EmissionSourceId: req.EmissionSourceId,
                    PollutantId: req.PollutantId,
                    Frequency: Frequency.Hourly
                )
            ]
        );

        var response = await Client.PutAsJsonAsync(
            $"{BaseRoute}/monitoring-plans/{_draftPlan.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ToResponseModel<MonitoringPlanDto>();
        dto.Version.Should().Be("v-updated");
        dto.MonitoringRequirements.First().Frequency.Should().Be(Frequency.Hourly);
    }


    [Fact]
    public async Task ShouldArchiveMonitoringPlan()
    {
        var response = await Client.PatchAsync(
            $"{BaseRoute}/monitoring-plans/{_activePlan.Id}/archive", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var db = await Context.Set<MonitoringPlan>().AsNoTracking()
            .FirstAsync(x => x.Id.Equals(_activePlan.Id));

        db.Status.Should().Be(MonitoringPlanStatus.Archived);
    }


    [Fact]
    public async Task ShouldNotActivateMonitoringPlan()
    {
        var response = await Client.PatchAsync(
            $"{BaseRoute}/monitoring-plans/{_draftPlan.Id}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var db = await Context.Set<MonitoringPlan>().AsNoTracking()
            .FirstAsync(x => x.Id.Equals(_draftPlan.Id));

        db.Status.Should().Be(MonitoringPlanStatus.Draft);
    }


    [Fact]
    public async Task ShouldNotArchivedMonitoringPlan()
    {
        var response = await Client.PatchAsync(
            $"{BaseRoute}/monitoring-plans/{_archivedPlan.Id}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var db = await Context.Set<MonitoringPlan>().AsNoTracking()
            .FirstAsync(x => x.Id.Equals(_draftPlan.Id));

        db.Status.Should().Be(MonitoringPlanStatus.Draft);
    }

    [Fact]
    public async Task ShouldDeleteMonitoringPlan()
    {
        var response = await Client.DeleteAsync(
            $"{BaseRoute}/monitoring-plans/{_draftPlan.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Context.Set<MonitoringPlan>().AnyAsync(x => x.Id.Equals(_draftPlan.Id))).Should().BeFalse();
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);

        await Context.Set<EmissionSource>().AddAsync(_source1);
        await Context.Set<EmissionSource>().AddAsync(_source2);

        await Context.Set<Pollutant>().AddAsync(_pollutant1);
        await Context.Set<Pollutant>().AddAsync(_pollutant2);

        await Context.Set<MonitoringPlan>().AddRangeAsync(_activePlan, _draftPlan, _archivedPlan);
        await Context.Set<MonitoringRequirement>().AddRangeAsync(_activeReqs.Concat(_draftReqs).Concat(_archivedReqs));

        await SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Context.Set<MonitoringRequirement>().RemoveRange(Context.Set<MonitoringRequirement>());
        Context.Set<MonitoringPlan>().RemoveRange(Context.Set<MonitoringPlan>());
        Context.Set<EmissionSource>().RemoveRange(Context.Set<EmissionSource>());
        Context.Set<Pollutant>().RemoveRange(Context.Set<Pollutant>());
        Context.Set<IedCategory>().RemoveRange(Context.Set<IedCategory>());
        Context.Set<MonitoringDevice>().RemoveRange(Context.Set<MonitoringDevice>());
        Context.Set<Installation>().RemoveRange(Context.Set<Installation>());
        Context.Set<Site>().RemoveRange(Context.Set<Site>());
        Context.Set<Enterprise>().RemoveRange(Context.Set<Enterprise>());
        Context.Set<Sector>().RemoveRange(Context.Set<Sector>());

        await SaveChangesAsync();
    }
}