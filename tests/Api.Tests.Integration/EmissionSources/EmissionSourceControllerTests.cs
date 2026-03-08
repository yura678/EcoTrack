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

namespace Api.Tests.Integration.EmissionSources;

public class EmissionSourceControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly Installation _installation;

    private readonly AirEmissionSource _airSource;
    private readonly WaterEmissionSource _waterSource;

    private readonly AirEmissionSource _airSourceToCreate;
    private readonly WaterEmissionSource _waterSourceToCreate;

    private const string BaseRoute = "api/v1";

    public EmissionSourceControllerTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);

        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);

        _airSource = EmissionSourcesData.FirstTestAirEmissionSource(_installation.Id);
        _waterSource = EmissionSourcesData.FirstTestWaterEmissionSource(_installation.Id);

        _airSourceToCreate = EmissionSourcesData.SecondTestAirEmissionSource(_installation.Id);
        _waterSourceToCreate = EmissionSourcesData.SecondTestWaterEmissionSource(_installation.Id);
    }

    [Fact]
    public async Task ShouldGetPagedEmissionSources()
    {
        // Arrange
        var url = $"api/v1/installations/{_installation.Id}/emission-sources?Page=1&PageSize=10";

        // Act
        var response = await Client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.ToResponseModel<PageResult<EmissionSourceDto>>();

        result.Items.Should().HaveCount(2);

        result.Items.Select(i => i.Id).Should().Contain(_airSource.Id);
        result.Items.Select(i => i.Id).Should().Contain(_waterSource.Id);

        result.Items.Should()
            .ContainSingle(i => i is AirEmissionSourceDto)
            .And
            .ContainSingle(i => i is WaterEmissionSourceDto);
    }


    [Fact]
    public async Task ShouldGetEmissionSourceById()
    {
        // Arrange
        var url = $"{BaseRoute}/emission-sources/{_airSource.Id}";

        // Act
        var response = await Client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ToResponseModel<AirEmissionSourceDto>();

        dto.Id.Should().Be(_airSource.Id);
        dto.Code.Should().Be(_airSource.Code);
        dto.InstallationId.Should().Be(_installation.Id);
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenSourceDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"{BaseRoute}/emission-sources/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }


    [Fact]
    public async Task ShouldCreateAirEmissionSource()
    {
        // Arrange
        var request = new CreateAirEmissionSourceDto(
            Code: _airSourceToCreate.Code,
            Height: _airSourceToCreate.Height,
            Diameter: _airSourceToCreate.Diameter,
            DesignFlowRate: _airSourceToCreate.DesignFlowRate
        );

        var url = $"api/v1/installations/{_installation.Id}/emission-sources/air";

        // Act
        var response = await Client.PostAsJsonAsync(url, request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var dto = await response.ToResponseModel<AirEmissionSourceDto>();

        var entity = await Context.Set<EmissionSource>()
            .FirstAsync(x => x.Id.Equals(dto.Id));

        entity.Code.Should().Be(request.Code);
        entity.InstallationId.Should().Be(_installation.Id);
    }

    [Fact]
    public async Task ShouldNotCreateAirEmissionSourceWhenCodeAlreadyExists()
    {
        // Arrange
        var request = new CreateAirEmissionSourceDto(
            Code: _airSource.Code,
            Height: _airSource.Height,
            Diameter: _airSource.Diameter,
            DesignFlowRate: _airSource.DesignFlowRate
        );

        var url = $"api/v1/installations/{_installation.Id}/emission-sources/air";

        // Act
        var response = await Client.PostAsJsonAsync(url, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ShouldFailCreateAirEmissionSourceWhenInvalid()
    {
        // Arrange
        var request = new CreateAirEmissionSourceDto(
            Code: "",
            Height: 0,
            Diameter: 0,
            DesignFlowRate: 0
        );

        var url = $"api/v1/installations/{_installation.Id}/emission-sources/air";

        // Act
        var response = await Client.PostAsJsonAsync(url, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }


    [Fact]
    public async Task ShouldCreateWaterEmissionSource()
    {
        // Arrange
        var request = new CreateWaterEmissionSourceDto(
            Code: _waterSourceToCreate.Code,
            Receiver: _waterSourceToCreate.Receiver,
            DesignFlowRate: _waterSourceToCreate.DesignFlowRate
        );

        var url = $"api/v1/installations/{_installation.Id}/emission-sources/water";

        // Act
        var response = await Client.PostAsJsonAsync(url, request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var dto = await response.ToResponseModel<WaterEmissionSourceDto>();

        var entity = await Context.Set<EmissionSource>().FirstAsync(x => x.Id.Equals(dto.Id));

        entity.Code.Should().Be(request.Code);
        entity.InstallationId.Should().Be(_installation.Id);
    }

    [Fact]
    public async Task ShouldNotCreateWaterEmissionSourceWhenCodeAlreadyExists()
    {
        // Arrange
        var request = new CreateWaterEmissionSourceDto(
            Code: _waterSource.Code,
            Receiver: _waterSource.Receiver,
            DesignFlowRate: _waterSource.DesignFlowRate
        );

        var url = $"api/v1/installations/{_installation.Id}/emission-sources/water";

        // Act
        var response = await Client.PostAsJsonAsync(url, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ShouldFailCreateWaterEmissionSourceWhenInvalid()
    {
        // Arrange
        var request = new CreateWaterEmissionSourceDto(
            Code: "",
            Receiver: "",
            DesignFlowRate: 0
        );

        var url = $"api/v1/installations/{_installation.Id}/emission-sources/water";

        // Act
        var response = await Client.PostAsJsonAsync(url, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }


    [Fact]
    public async Task ShouldUpdateAirEmissionSource()
    {
        // Arrange
        var request = new UpdateAirEmissionSourceDto(
            Height: _airSourceToCreate.Height,
            Diameter: _airSourceToCreate.Diameter,
            DesignFlowRate: _airSourceToCreate.DesignFlowRate
        );

        var url = $"{BaseRoute}/emission-sources/{_airSource.Id}/air";

        // Act
        var response = await Client.PutAsJsonAsync(url, request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var updated = await Context.Set<EmissionSource>().AsNoTracking()
            .FirstAsync(x => x.Id.Equals(_airSource.Id));

        var air = (AirEmissionSource)updated;

        air.Height.Should().Be(request.Height);
        air.Diameter.Should().Be(request.Diameter);
        air.DesignFlowRate.Should().Be(request.DesignFlowRate);
    }


    [Fact]
    public async Task ShouldUpdateWaterEmissionSource()
    {
        // Arrange
        var request = new UpdateWaterEmissionSourceDto(
            Receiver: _waterSourceToCreate.Receiver,
            DesignFlowRate: _waterSourceToCreate.DesignFlowRate
        );

        var url = $"{BaseRoute}/emission-sources/{_waterSource.Id}/water";

        // Act
        var response = await Client.PutAsJsonAsync(url, request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var updated = await Context.Set<EmissionSource>().AsNoTracking()
            .FirstAsync(x => x.Id.Equals(_waterSource.Id));

        var water = (WaterEmissionSource)updated;

        water.Receiver.Should().Be(request.Receiver);
        water.DesignFlowRate.Should().Be(request.DesignFlowRate);
    }


    [Fact]
    public async Task ShouldDeleteEmissionSource()
    {
        // Arrange
        var url = $"{BaseRoute}/emission-sources/{_airSource.Id}";

        // Act
        var response = await Client.DeleteAsync(url);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var deleted = await Context.Set<EmissionSource>()
            .FirstOrDefaultAsync(x => x.Id.Equals(_airSource.Id));

        deleted.Should().BeNull();
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenDeletingNonExisting()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"{BaseRoute}/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);

        await Context.Set<IedCategory>().AddAsync(_iedCategory);

        await Context.Set<Installation>().AddAsync(_installation);

        await Context.Set<EmissionSource>().AddAsync(_airSource);
        await Context.Set<EmissionSource>().AddAsync(_waterSource);

        await SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Context.Set<EmissionSource>().RemoveRange(Context.Set<EmissionSource>());
        Context.Set<IedCategory>().RemoveRange(Context.Set<IedCategory>());
        Context.Set<MonitoringDevice>().RemoveRange(Context.Set<MonitoringDevice>());

        Context.Set<Installation>().RemoveRange(Context.Set<Installation>());
        Context.Set<Site>().RemoveRange(Context.Set<Site>());
        Context.Set<Enterprise>().RemoveRange(Context.Set<Enterprise>());
        Context.Set<Sector>().RemoveRange(Context.Set<Sector>());

        await SaveChangesAsync();
    }
}