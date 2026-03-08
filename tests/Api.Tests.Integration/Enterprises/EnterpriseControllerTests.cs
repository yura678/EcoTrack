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

public class EnterpriseControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _firstTestSector = SectorsData.FirstTestSector();
    private readonly Site _firstTestSite;

    private readonly Enterprise _firstTestEnterprise;
    private readonly Enterprise _secondTestEnterprise;
    private readonly Enterprise _thirdTestEnterprise;

    private const string BaseRoute = "api/v1/enterprises";
    private readonly string _getRoute;

    public EnterpriseControllerTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _firstTestEnterprise = EnterprisesData.FirstTestEquipment(_firstTestSector.Id);
        _secondTestEnterprise = EnterprisesData.SecondTestEquipment(_firstTestSector.Id);
        _thirdTestEnterprise = EnterprisesData.ThirdTestEquipment(_firstTestSector.Id);

        _firstTestSite = SitesData.FirstTestSite(_thirdTestEnterprise.Id);

        _getRoute = $"{BaseRoute}/{_firstTestEnterprise.Id}";
    }

    [Fact]
    public async Task ShouldGetEnterpriseById()
    {
        // Act
        var response = await Client.GetAsync(_getRoute);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var equipmentDto = await response.ToResponseModel<EnterpriseDto>();
        equipmentDto.Id.Should().Be(_firstTestEnterprise.Id);
        equipmentDto.Name.Should().Be(_firstTestEnterprise.Name);
        equipmentDto.Edrpou.Should().Be(_firstTestEnterprise.Edrpou);
        equipmentDto.RiskGroup.Should().Be(_firstTestEnterprise.RiskGroup);
        equipmentDto.Address.Should().Be(_firstTestEnterprise.Address);
        equipmentDto.SectorId.Should().Be(_firstTestEnterprise.SectorId);
        equipmentDto.CreatedAt.Should().BeCloseTo(_firstTestEnterprise.CreatedAt, TimeSpan.FromSeconds(1));
        equipmentDto.CreatedAt.Should().BeCloseTo(_firstTestEnterprise.CreatedAt, TimeSpan.FromSeconds(1));
        equipmentDto.UpdatedAt.Should().Be(null);
    }

    [Fact]
    public async Task ShouldCreateEnterprise()
    {
        // Arrange
        var request = new CreateEnterpriseDto(
            Name: _secondTestEnterprise.Name,
            Edrpou: _secondTestEnterprise.Edrpou,
            RiskGroup: _secondTestEnterprise.RiskGroup,
            Address: _secondTestEnterprise.Address,
            SectorId: _secondTestEnterprise.SectorId
        );

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var equipmentDto = await response.ToResponseModel<EnterpriseDto>();

        var dbEnterprise = await Context.Set<Enterprise>().FirstAsync(x => x.Id.Equals(equipmentDto.Id));

        dbEnterprise.Name.Should().Be(request.Name);
        dbEnterprise.Edrpou.Should().Be(request.Edrpou);
        dbEnterprise.RiskGroup.Should().Be(request.RiskGroup);
        dbEnterprise.Address.Should().Be(request.Address);
        dbEnterprise.SectorId.Should().Be(request.SectorId);
    }

    [Fact]
    public async Task ShouldNotCreateEquipmentBecauseEdrpouDuplication()
    {
        // Arrange
        var request = new CreateEnterpriseDto(
            Name: _secondTestEnterprise.Name,
            Edrpou: _firstTestEnterprise.Edrpou,
            RiskGroup: _secondTestEnterprise.RiskGroup,
            Address: _secondTestEnterprise.Address,
            SectorId: _secondTestEnterprise.SectorId
        );

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public async Task ShouldNotCreateEnterpriseWhenNameIsInvalid(int count)
    {
        // Arrange
        var invalidName = new string('1', count);

        var request = new CreateEnterpriseDto(
            Name: invalidName,
            Edrpou: _secondTestEnterprise.Edrpou,
            RiskGroup: _secondTestEnterprise.RiskGroup,
            Address: _secondTestEnterprise.Address,
            SectorId: _secondTestEnterprise.SectorId
        );

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }


    [Theory]
    [InlineData("1234567")]
    [InlineData("abcdefgh")]
    [InlineData("123456789")]
    public async Task ShouldNotCreateEnterpriseWhenEdrpouIsInvalid(string invalidEdrpou)
    {
        // Arrange
        var request = new CreateEnterpriseDto(
            Name: _secondTestEnterprise.Name,
            Edrpou: invalidEdrpou,
            RiskGroup: _secondTestEnterprise.RiskGroup,
            Address: _secondTestEnterprise.Address,
            SectorId: _secondTestEnterprise.SectorId
        );

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }


    [Fact]
    public async Task ShouldUpdateEnterprise()
    {
        // Arrange
        var id = _firstTestEnterprise.Id;

        var request = new UpdateEnterpriseDto(
            Name: _secondTestEnterprise.Name,
            RiskGroup: _secondTestEnterprise.RiskGroup,
            Address: _secondTestEnterprise.Address,
            SectorId: _secondTestEnterprise.SectorId
        );

        // Act
        var response = await Client.PutAsJsonAsync($"{BaseRoute}/{id}", request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var updatedEnterpriseDto = await response.ToResponseModel<EnterpriseDto>();

        var enterpriseId = updatedEnterpriseDto.Id;
        var dbEnterprise = await Context.Set<Enterprise>().AsNoTracking().FirstAsync(x => x.Id.Equals(enterpriseId));

        dbEnterprise.Name.Should().Be(request.Name);
        dbEnterprise.RiskGroup.Should().Be(request.RiskGroup);
        dbEnterprise.Address.Should().Be(request.Address);
        dbEnterprise.SectorId.Should().Be(request.SectorId);
    }


    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public async Task ShouldNotUpdateEnterpriseWhenNameIsInvalid(int count)
    {
        // Arrange
        var id = _firstTestEnterprise.Id;
        var invalidName = new string('1', count);

        var request = new UpdateEnterpriseDto(
            Name: invalidName,
            RiskGroup: _secondTestEnterprise.RiskGroup,
            Address: _secondTestEnterprise.Address,
            SectorId: _secondTestEnterprise.SectorId
        );

        // Act
        var response = await Client.PutAsJsonAsync($"{BaseRoute}/{id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ShouldNotUpdateEnterpriseWhenRiskGroupIsInvalid()
    {
        // Arrange
        var id = _firstTestEnterprise.Id;

        var request = new UpdateEnterpriseDto(
            Name: _secondTestEnterprise.Name,
            RiskGroup: (RiskGroup)100,
            Address: _secondTestEnterprise.Address,
            SectorId: _secondTestEnterprise.SectorId
        );

        // Act
        var response = await Client.PutAsJsonAsync($"{BaseRoute}/{id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenEnterpriseDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateEnterpriseDto(
            Name: _secondTestEnterprise.Name,
            RiskGroup: _secondTestEnterprise.RiskGroup,
            Address: _secondTestEnterprise.Address,
            SectorId: _secondTestEnterprise.SectorId
        );
        // Act
        var response = await Client.PutAsJsonAsync($"{BaseRoute}/{id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenSectorDoesNotExist()
    {
        // Arrange
        var id = _firstTestEnterprise.Id;
        var sectorId = Guid.NewGuid();

        var request = new UpdateEnterpriseDto(
            Name: _secondTestEnterprise.Name,
            RiskGroup: _secondTestEnterprise.RiskGroup,
            Address: _secondTestEnterprise.Address,
            SectorId: sectorId
        );
        // Act
        var response = await Client.PutAsJsonAsync($"{BaseRoute}/{id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShouldDeleteEnterprise()
    {
        // Arrange
        var id = _firstTestEnterprise.Id;
        // Act
        var response = await Client.DeleteAsync($"{BaseRoute}/{id}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var deletedEnterprise =
            await Context.Set<Enterprise>().FirstOrDefaultAsync(x => x.Id.Equals(_firstTestEnterprise.Id));
        deletedEnterprise.Should().BeNull();
    }

    [Fact]
    public async Task ShouldNotDeleteEnterprise()
    {
        // Arrange
        var id = _thirdTestEnterprise.Id;
        // Act

        var response = await Client.DeleteAsync($"{BaseRoute}/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }


    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_firstTestSector);
        await Context.Set<Enterprise>().AddAsync(_firstTestEnterprise);

        await Context.Set<Enterprise>().AddAsync(_thirdTestEnterprise);
        await Context.Set<Site>().AddAsync(_firstTestSite);
        await SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.Set<MonitoringDevice>().ExecuteDeleteAsync();
        await Context.Set<Installation>().ExecuteDeleteAsync();
        await Context.Set<Site>().ExecuteDeleteAsync();
        await Context.Set<Enterprise>().ExecuteDeleteAsync();
        await Context.Set<Sector>().ExecuteDeleteAsync();
    }
}