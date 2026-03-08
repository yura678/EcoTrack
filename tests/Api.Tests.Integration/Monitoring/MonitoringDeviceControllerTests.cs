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

public class MonitoringDeviceControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _ied;
    private readonly Installation _installation;
    private readonly EmissionSource _emissionSource;

    private readonly MonitoringDevice _firstDevice;
    private readonly MonitoringDevice _secondDevice;

    private const string BaseRoute = "api/v1";

    public MonitoringDeviceControllerTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _ied = IedCategoriesData.FirstTestIedCategory();
        _installation = InstallationData.FirstTestInstallation(_site.Id, _ied.Id);
        _emissionSource = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);

        _firstDevice = MonitoringDevicesData.FirstTestDevice(_emissionSource.Id, _installation.Id);
        _secondDevice = MonitoringDevicesData.SecondTestDevice(_emissionSource.Id, _installation.Id);
    }


    [Fact]
    public async Task ShouldGetDevicesOfInstallation()
    {
        // Arrange
        var url = $"{BaseRoute}/installations/{_installation.Id}/monitoring-devices?&Page=1&PageSize=10";

        // Act
        var response = await Client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ToResponseModel<PageResult<MonitoringDeviceDto>>();

        page.Items.Should().HaveCount(2);
        page.Items.Should().Contain(x => x.Id == _firstDevice.Id);
        page.Items.Should().Contain(x => x.Id == _secondDevice.Id);
    }


    [Fact]
    public async Task ShouldGetDeviceById()
    {
        // Arrange
        var url = $"{BaseRoute}/monitoring-devices/{_firstDevice.Id}";

        // Act
        var response = await Client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ToResponseModel<MonitoringDeviceDto>();

        dto.Id.Should().Be(_firstDevice.Id);
        dto.EmissionSourceId.Should().Be(_emissionSource.Id);
        dto.Model.Should().Be(_firstDevice.Model);
        dto.SerialNumber.Should().Be(_firstDevice.SerialNumber);
        dto.Type.Should().Be(_firstDevice.Type);
        dto.IsOnline.Should().Be(_firstDevice.IsOnline);
    }

    [Fact]
    public async Task ShouldReturnNotFoundForMissingDevice()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"{BaseRoute}/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }


    [Fact]
    public async Task ShouldCreateMonitoringDevice()
    {
        // Arrange
        var model = MonitoringDevicesData.DeviceToCreate(_emissionSource.Id, _installation.Id);

        var request = new CreateMonitoringDeviceDto(
            EmissionSourceId: model.EmissionSourceId,
            Model: model.Model,
            SerialNumber: model.SerialNumber,
            Type: model.Type,
            IsOnline: model.IsOnline,
            Notes: model.Notes
        );

        var url = $"{BaseRoute}/installations/{_installation.Id}/monitoring-devices";


        // Act
        var response = await Client.PostAsJsonAsync(url, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ToResponseModel<MonitoringDeviceDto>();


        var entity = await Context.Set<MonitoringDevice>().AsNoTracking()
            .FirstAsync(x => x.Id.Equals(dto.Id));

        entity.Model.Should().Be(model.Model);
        entity.SerialNumber.Should().Be(model.SerialNumber);
    }

    [Fact]
    public async Task ShouldNotCreateWhenModelInvalid()
    {
        // Arrange
        var request = new CreateMonitoringDeviceDto(
            EmissionSourceId: _emissionSource.Id,
            Model: "",
            SerialNumber: "SN-1",
            Type: MonitoringDeviceType.CEMS,
            IsOnline: true,
            Notes: null
        );

        var url = $"{BaseRoute}/installations/{_installation.Id}/monitoring-devices";

        // Act
        var response = await Client.PostAsJsonAsync(url, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ShouldNotCreateWhenEmissionSourceMissing()
    {
        // Arrange
        var request = new CreateMonitoringDeviceDto(
            EmissionSourceId: Guid.NewGuid(),
            Model: "CEMS-200",
            SerialNumber: "S-999",
            Type: MonitoringDeviceType.CEMS,
            IsOnline: true,
            Notes: null
        );

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }


    [Fact]
    public async Task ShouldUpdateMonitoringDevice()
    {
        // Arrange
        var update = new UpdateMonitoringDeviceDto(
            EmissionSourceId: _emissionSource.Id,
            IsOnline: false,
            Notes: "updated"
        );

        var url = $"{BaseRoute}/monitoring-devices/{_firstDevice.Id}";

        // Act
        var response = await Client.PutAsJsonAsync(url, update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var db = await Context.Set<MonitoringDevice>().AsNoTracking()
            .FirstAsync(x => x.Id.Equals(_firstDevice.Id));

        db.IsOnline.Should().BeFalse();
        db.Notes.Should().Be("updated");
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenUpdatingMissingDevice()
    {
        // Arrange
        var id = Guid.NewGuid();

        var update = new UpdateMonitoringDeviceDto(
            EmissionSourceId: _emissionSource.Id,
            IsOnline: true,
            Notes: "abc"
        );

        // Act
        var response = await Client.PutAsJsonAsync($"{BaseRoute}/monitoring-devices/{id}", update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShouldNotUpdateWhenInvalidData()
    {
        // Arrange
        var update = new UpdateMonitoringDeviceDto(
            EmissionSourceId: _emissionSource.Id,
            IsOnline: true,
            Notes: new string('x', 2000)
        );

        var url = $"{BaseRoute}/monitoring-devices/{_firstDevice.Id}";

        // Act
        var response = await Client.PutAsJsonAsync(url, update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }


    [Fact]
    public async Task ShouldDeleteMonitoringDevice()
    {
        // Arrange
        var url = $"{BaseRoute}/monitoring-devices/{_secondDevice.Id}";

        // Act
        var response = await Client.DeleteAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var exists = await Context.Set<MonitoringDevice>()
            .AnyAsync(x => x.Id.Equals(_secondDevice.Id));

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenDeletingMissingDevice()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"{BaseRoute}/monitoring-devices/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }


    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_ied);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_emissionSource);

        await Context.Set<MonitoringDevice>().AddRangeAsync(_firstDevice, _secondDevice);

        await SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Context.Set<MonitoringDevice>().RemoveRange(Context.Set<MonitoringDevice>());
        Context.Set<EmissionSource>().RemoveRange(Context.Set<EmissionSource>());
        Context.Set<Installation>().RemoveRange(Context.Set<Installation>());
        Context.Set<IedCategory>().RemoveRange(Context.Set<IedCategory>());
        Context.Set<Site>().RemoveRange(Context.Set<Site>());
        Context.Set<Enterprise>().RemoveRange(Context.Set<Enterprise>());
        Context.Set<Sector>().RemoveRange(Context.Set<Sector>());

        await SaveChangesAsync();
    }
}