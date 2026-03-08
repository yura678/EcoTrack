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

namespace Api.Tests.Integration.Enterprises;

public class InstallationControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;

    private readonly IedCategory _iedCategory;
    private readonly Installation _firstInstallation;

    private readonly Installation _secondInstallation;
    private readonly EmissionSource _firstEmissionSource;


    private const string BaseRoute = "api/v1/installations";

    public InstallationControllerTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);

        _iedCategory = IedCategoriesData.FirstTestIedCategory();
        _firstInstallation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _secondInstallation = InstallationData.SecondTestInstallation(_site.Id, _iedCategory.Id);
        _firstEmissionSource = EmissionSourcesData.FirstTestEmissionSource(_firstInstallation.Id);
    }


    [Fact]
    public async Task ShouldGetInstallationsBySite()
    {
        // Arrange
        var route = $"api/v1/sites/{_site.Id}/installations";

        // Act
        var response = await Client.GetAsync(route);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await response.ToResponseModel<List<InstallationDto>>();
        items.Should().HaveCount(2);
        items.Should().Contain(i => i.Id == _firstInstallation.Id);
        items.Should().Contain(i => i.Id == _secondInstallation.Id);
    }

    [Fact]
    public async Task ShouldGetInstallationById()
    {
        // Arrange
        var route = $"{BaseRoute}/{_firstInstallation.Id}?includeEmissionSources=false";

        // Act
        var response = await Client.GetAsync(route);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ToResponseModel<InstallationDto>();

        dto.Id.Should().Be(_firstInstallation.Id);
        dto.Name.Should().Be(_firstInstallation.Name);
        dto.IedCategoryId.Should().Be(_firstInstallation.IedCategoryId);
        dto.SiteId.Should().Be(_firstInstallation.SiteId);
        dto.Status.Should().Be(_firstInstallation.Status);
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenInstallationDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();
        var route = $"{BaseRoute}/{id}";

        // Act
        var response = await Client.GetAsync(route);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }


    [Fact]
    public async Task ShouldCreateInstallation()
    {
        // Arrange
        var request = new CreateInstallationDto(
            Name: _secondInstallation.Name,
            IedCategoryId: _iedCategory.Id,
            SiteId: _site.Id,
            Status: _secondInstallation.Status
        );

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var dto = await response.ToResponseModel<InstallationDto>();

        var entity = await Context.Set<Installation>()
            .FirstAsync(x => x.Id.Equals(dto.Id));

        entity.Name.Should().Be(request.Name);
        entity.IedCategoryId.Should().Be(request.IedCategoryId);
        entity.SiteId.Should().Be(request.SiteId);
        entity.Status.Should().Be(request.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ShouldNotCreateInstallationWhenNameInvalid(string invalidName)
    {
        // Arrange
        var request = new CreateInstallationDto(
            Name: invalidName,
            IedCategoryId: _iedCategory.Id,
            SiteId: _site.Id,
            Status: _firstInstallation.Status
        );

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ShouldNotCreateInstallationWhenIedCategoryInvalid()
    {
        // Arrange
        var request = new CreateInstallationDto(
            Name: _firstInstallation.Name,
            IedCategoryId: Guid.NewGuid(), // invalid
            SiteId: _site.Id,
            Status: _firstInstallation.Status
        );

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShouldNotCreateInstallationWhenSiteInvalid()
    {
        // Arrange
        var request = new CreateInstallationDto(
            Name: _firstInstallation.Name,
            IedCategoryId: _iedCategory.Id,
            SiteId: Guid.NewGuid(), // invalid
            Status: _firstInstallation.Status
        );

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShouldUpdateInstallation()
    {
        // Arrange
        var newData = InstallationData.ThirdTestInstallation(_site.Id, _iedCategory.Id);

        var request = new UpdateInstallationDto(
            Name: newData.Name,
            IedCategoryId: newData.IedCategoryId
        );

        var route = $"{BaseRoute}/{_firstInstallation.Id}";

        // Act
        var response = await Client.PutAsJsonAsync(route, request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var entity = await Context.Set<Installation>().AsNoTracking()
            .FirstAsync(x => x.Id.Equals(_firstInstallation.Id));

        entity.Name.Should().Be(request.Name);
        entity.IedCategoryId.Should().Be(request.IedCategoryId);
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenUpdatingNonExistingInstallation()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateInstallationDto(
            Name: _firstInstallation.Name,
            IedCategoryId: _iedCategory.Id
        );

        // Act
        var response = await Client.PutAsJsonAsync($"{BaseRoute}/{id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShouldNotUpdateInstallationWhenNameInvalid()
    {
        // Arrange
        var request = new UpdateInstallationDto(
            Name: "",
            IedCategoryId: _iedCategory.Id
        );

        // Act
        var response = await Client.PutAsJsonAsync($"{BaseRoute}/{_firstInstallation.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }


    [Fact]
    public async Task ShouldUpdateInstallationStatus()
    {
        // Arrange
        var request = new UpdateInstallationStatusDto(
            Status: InstallationStatus.Decommissioned
        );

        var route = $"{BaseRoute}/{_firstInstallation.Id}";

        // Act
        var response = await Client.PatchAsJsonAsync(route, request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var entity = await Context.Set<Installation>().AsNoTracking()
            .FirstAsync(x => x.Id.Equals(_firstInstallation.Id));

        entity.Status.Should().Be(InstallationStatus.Decommissioned);
    }

    [Fact]
    public async Task ShouldNotUpdateStatusWhenInstallationDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();
        
        var request = new UpdateInstallationStatusDto(
            Status: InstallationStatus.UnderConstruction
        );

        // Act
        var response = await Client.PatchAsJsonAsync($"{BaseRoute}/{id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }


    [Fact]
    public async Task ShouldDeleteInstallation()
    {
        // Arrange
        var route = $"{BaseRoute}/{_secondInstallation.Id}";

        // Act
        var response = await Client.DeleteAsync(route);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var entity = await Context.Set<Installation>()
            .FirstOrDefaultAsync(x => x.Id.Equals(_secondInstallation.Id));

        entity.Should().BeNull();
    }

    [Fact]
    public async Task ShouldDeleteInstallationWhenHasEmissionSources()
    {
        // Arrange
        var route = $"{BaseRoute}/{_firstInstallation.Id}";

        // Act
        var response = await Client.DeleteAsync(route);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var entity = await Context.Set<Installation>()
            .FirstOrDefaultAsync(x => x.Id.Equals(_firstInstallation.Id));

        entity.Should().BeNull();
    }


    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);

        await Context.Set<IedCategory>().AddAsync(_iedCategory);

        await Context.Set<Installation>().AddAsync(_firstInstallation);
        await Context.Set<Installation>().AddAsync(_secondInstallation);

        await Context.Set<EmissionSource>().AddAsync(_firstEmissionSource);

        await SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Context.Set<EmissionSource>().RemoveRange(Context.Set<EmissionSource>());
        Context.Set<MonitoringDevice>().RemoveRange(Context.Set<MonitoringDevice>());
        Context.Set<Installation>().RemoveRange(Context.Set<Installation>());
        Context.Set<IedCategory>().RemoveRange(Context.Set<IedCategory>());
        Context.Set<Site>().RemoveRange(Context.Set<Site>());
        Context.Set<Enterprise>().RemoveRange(Context.Set<Enterprise>());
        Context.Set<Sector>().RemoveRange(Context.Set<Sector>());

        await SaveChangesAsync();
    }
}