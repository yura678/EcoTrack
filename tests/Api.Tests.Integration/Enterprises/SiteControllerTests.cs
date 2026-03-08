using System.Net;
using System.Net.Http.Json;
using Api.Dtos;

using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tests.Common;
using Tests.Data.Enterprises;

namespace Api.Tests.Integration.Enterprises;

public class SiteControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _testSector = SectorsData.FirstTestSector();
    private readonly Enterprise _testEnterprise;
    private readonly Site _firstSite;
    private readonly Site _secondSite;
    private readonly IedCategory _firstIedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _firstInstallation;

    private const string BaseRoute = "api/v1/sites";

    public SiteControllerTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _testEnterprise = EnterprisesData.FirstTestEquipment(_testSector.Id);

        _firstSite = SitesData.FirstTestSite(_testEnterprise.Id);
        _secondSite = SitesData.SecondTestSite(_testEnterprise.Id);

        _firstInstallation = InstallationData.FirstTestInstallation(_firstSite.Id, _firstIedCategory.Id);
    }
    
    [Fact]
    public async Task ShouldGetSitesOfEnterprise()
    {
        // Arrange
        var route = $"api/v1/enterprises/{_testEnterprise.Id}/sites";

        // Act
        var response = await Client.GetAsync(route);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sites = await response.ToResponseModel<List<SiteDto>>();
        sites.Should().HaveCount(2);

        sites.Should().ContainSingle(s => s.Id == _firstSite.Id);
        sites.Should().ContainSingle(s => s.Id == _secondSite.Id);
    }
    
    [Fact]
    public async Task ShouldGetSiteById()
    {
        // Arrange
        var route = $"{BaseRoute}/{_firstSite.Id}";

        // Act
        var response = await Client.GetAsync(route);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ToResponseModel<SiteDto>();

        dto.Id.Should().Be(_firstSite.Id);
        dto.Name.Should().Be(_firstSite.Name);
        dto.Address.Should().Be(_firstSite.Address);
        dto.SanitaryZoneRadius.Should().Be(_firstSite.SanitaryZoneRadius);
        dto.EnterpriseId.Should().Be(_testEnterprise.Id);
        dto.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenSiteDoesNotExist()
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
    public async Task ShouldCreateSite()
    {
        // Arrange
        var request = new CreateSiteDto(
            Name: _secondSite.Name,
            Address: _secondSite.Address,
            SanitaryZoneRadius: _secondSite.SanitaryZoneRadius!.Value,
            EnterpriseId: _testEnterprise.Id);

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var dto = await response.ToResponseModel<SiteDto>();
        var db = await Context.Set<Site>().FirstAsync(x => x.Id.Equals(dto.Id));

        db.Name.Should().Be(request.Name);
        db.Address.Should().Be(request.Address);
        db.SanitaryZoneRadius.Should().Be(request.SanitaryZoneRadius);
        db.EnterpriseId.Should().Be(request.EnterpriseId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ShouldNotCreateSiteWhenNameInvalid(string invalid)
    {
        // Arrange
        var request = new CreateSiteDto(
            Name: invalid,
            Address: _firstSite.Address,
            SanitaryZoneRadius: _firstSite.SanitaryZoneRadius!.Value,
            EnterpriseId: _testEnterprise.Id);

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ShouldNotCreateSiteWhenAddressInvalid(string? invalid)
    {
        // Arrange
        var request = new CreateSiteDto(
            Name: _firstSite.Name,
            Address: invalid!,
            SanitaryZoneRadius: _firstSite.SanitaryZoneRadius!.Value,
            EnterpriseId: _testEnterprise.Id);

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ShouldNotCreateSiteWhenRadiusInvalid()
    {
        // Arrange
        var request = new CreateSiteDto(
            Name: _firstSite.Name,
            Address: _firstSite.Address,
            SanitaryZoneRadius: 0,
            EnterpriseId: _testEnterprise.Id);

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ShouldNotCreateSiteWhenEnterpriseNotFound()
    {
        // Arrange
        var request = new CreateSiteDto(
            Name: _firstSite.Name,
            Address: _firstSite.Address,
            SanitaryZoneRadius: _firstSite.SanitaryZoneRadius!.Value,
            EnterpriseId: Guid.NewGuid());

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

 
    [Fact]
    public async Task ShouldUpdateSite()
    {
        // Arrange
        var request = new UpdateSiteDto(
            Name: _secondSite.Name,
            Address: _secondSite.Address,
            SanitaryZoneRadius: _secondSite.SanitaryZoneRadius!.Value);

        var url = $"{BaseRoute}/{_firstSite.Id}";

        // Act
        var response = await Client.PutAsJsonAsync(url, request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var db = await Context.Set<Site>().AsNoTracking().FirstAsync(x => x.Id.Equals(_firstSite.Id));

        db.Name.Should().Be(request.Name);
        db.Address.Should().Be(request.Address);
        db.SanitaryZoneRadius.Should().Be(request.SanitaryZoneRadius);
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenUpdatingNonExistingSite()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateSiteDto(
            Name: _firstSite.Name,
            Address: _firstSite.Address,
            SanitaryZoneRadius: _firstSite.SanitaryZoneRadius!.Value);

        // Act
        var response = await Client.PutAsJsonAsync($"{BaseRoute}/{id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShouldNotUpdateSiteWhenNameInvalid()
    {
        // Arrange
        var request = new UpdateSiteDto(
            Name: "",
            Address: _firstSite.Address,
            SanitaryZoneRadius: _firstSite.SanitaryZoneRadius!.Value);

        // Act
        var response = await Client.PutAsJsonAsync($"{BaseRoute}/{_firstSite.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

  
    [Fact]
    public async Task ShouldDeleteSite()
    {
        // Arrange
        var route = $"{BaseRoute}/{_secondSite.Id}";

        // Act
        var response = await Client.DeleteAsync(route);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var db = await Context.Set<Site>().FirstOrDefaultAsync(x => x.Id.Equals(_secondSite.Id));
        db.Should().BeNull();
    }

    [Fact]
    public async Task ShouldNotDeleteSiteWhenInstallationsExist()
    {
        // Arrange
        var route = $"{BaseRoute}/{_firstSite.Id}";

        // Act
        var response = await Client.DeleteAsync(route);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

   
    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_testSector);
        await Context.Set<Enterprise>().AddAsync(_testEnterprise);
        await Context.Set<IedCategory>().AddAsync(_firstIedCategory);
        await Context.Set<Installation>().AddAsync(_firstInstallation);
        await Context.Set<Site>().AddRangeAsync(_firstSite, _secondSite);

        await SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Context.Set<MonitoringDevice>().RemoveRange(Context.Set<MonitoringDevice>());
        Context.Set<Installation>().RemoveRange(Context.Set<Installation>());
        Context.Set<IedCategory>().RemoveRange(Context.Set<IedCategory>());
        Context.Set<Site>().RemoveRange(Context.Set<Site>());
        Context.Set<Enterprise>().RemoveRange(Context.Set<Enterprise>());
        Context.Set<Sector>().RemoveRange(Context.Set<Sector>());

        await SaveChangesAsync();
    }
}