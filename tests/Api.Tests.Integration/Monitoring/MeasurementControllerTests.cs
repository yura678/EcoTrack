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

public class MeasurementControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;
    private readonly EmissionSource _emissionSource;

    private readonly Pollutant _pollutant;
    private readonly MeasureUnit _mg;
    private readonly MeasureUnit _g;
    private readonly MeasureUnit _ug;

    private readonly MonitoringDevice _device;

    private readonly MonitoringPlan _plan;
    private readonly MonitoringRequirement _requirement;

    private readonly Permit _multiPermit;
    private readonly EmissionLimit[] _limits3;


    private readonly Measurement _seedMeasurement;

    private const string BaseRoute = "api/v1/measurements";

    public MeasurementControllerTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _emissionSource = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);

        _pollutant = PollutantsData.FirstTestPollutant();
        _mg = MeasureUnitsData.MgPerM3();
        _g = MeasureUnitsData.GPerM3();
        _ug = MeasureUnitsData.UgPerM3();
        _device = MonitoringDevicesData.FirstTestDevice(_emissionSource.Id, _installation.Id);

        var planId = Guid.NewGuid();

        _requirement = MonitoringRequirementsData.FirstTestRequirement(
            planId,
            _emissionSource.Id,
            _pollutant.Id);

        _plan = MonitoringPlansData.FirstActiveTestPlan(
            planId,
            _installation.Id,
            [_requirement]);

        var multi = PermitsBundlesData.FirstMultiLimitPermit(
            _installation.Id,
            _pollutant.Id,
            _emissionSource.Id,
            _mg,
            _g,
            _ug);

        _multiPermit = multi.Permit;
        _limits3 = multi.Limits;

        _seedMeasurement = MeasurementsData.FirstSeedMeasurement(
            _emissionSource.Id,
            _pollutant.Id,
            _device.Id,
            _mg.Id);
    }


    [Fact]
    public async Task ShouldGetPagedMeasurements()
    {
        // Arrange
        var url =
            $"{BaseRoute}?InstallationId={_installation.Id}&Page=1&PageSize=10";

        // Act
        var response = await Client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.ToResponseModel<PageResult<MeasurementDto>>();

        page.Items.Should().NotBeEmpty();
        page.Items.Should().Contain(m => m.Id == _seedMeasurement.Id);

        page.Page.Should().Be(1);
        page.PageSize.Should().Be(10);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ShouldGetMeasurementById()
    {
        // Arrange
        var route = $"{BaseRoute}/{_seedMeasurement.Id}";

        // Act
        var response = await Client.GetAsync(route);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ToResponseModel<MeasurementDto>();

        dto.Id.Should().Be(_seedMeasurement.Id);
        dto.EmissionSourceId.Should().Be(_emissionSource.Id);
        dto.PollutantId.Should().Be(_pollutant.Id);
        dto.DeviceId.Should().Be(_device.Id);
        dto.UnitId.Should().Be(_mg.Id);
        dto.Value.Should().Be(_seedMeasurement.Value);
        dto.Period.Should().Be(_seedMeasurement.Period);
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenMeasurementDoesNotExist()
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
    public async Task ShouldCreateMeasurementBelowLimit()
    {
        // Arrange
        var value = 50m - 10m;

        var request = new CreateMeasurementDto(
            Timestamp: DateTime.UtcNow.AddMinutes(-10),
            EmissionSourceId: _emissionSource.Id,
            PollutantId: _pollutant.Id,
            DeviceId: _device.Id,
            UnitId: _mg.Id,
            Period: AveragingWindow.OneHour,
            Value: value
        );

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var dto = await response.ToResponseModel<MeasurementDto>();


        var dbMeasurement = await Context.Set<Measurement>()
            .AsNoTracking()
            .FirstAsync(x => x.Id.Equals(dto.Id));

        dbMeasurement.Value.Should().Be(request.Value);
        dbMeasurement.Status.Should().Be(MeasurementStatus.Valid);

        // Перевіряємо, що перевищень нема
        var exceedances = await Context.Set<ExceedanceEvent>()
            .Where(e => e.MeasurementId.Equals(dto.Id))
            .ToListAsync();

        exceedances.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldCreateMeasurementAboveLimitAndCreateExceedanceEvent()
    {
        // Arrange
        var value = 50m + 10m;

        var request = new CreateMeasurementDto(
            Timestamp: DateTime.UtcNow.AddMinutes(-5),
            EmissionSourceId: _emissionSource.Id,
            PollutantId: _pollutant.Id,
            DeviceId: _device.Id,
            UnitId: _mg.Id,
            Period: AveragingWindow.OneHour,
            Value: value
        );

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var dto = await response.ToResponseModel<MeasurementDto>();
        var measurementId = dto.Id;

        var dbMeasurement = await Context.Set<Measurement>()
            .AsNoTracking()
            .FirstAsync(x => x.Id.Equals(dto.Id));

        dbMeasurement.Value.Should().Be(request.Value);

        var exceedances = await Context.Set<ExceedanceEvent>()
            .Where(e => e.MeasurementId.Equals(dto.Id))
            .ToListAsync();

        exceedances.Should().NotBeEmpty();
        exceedances.Should().OnlyContain(e => e.Status == ExceedanceEventStatus.Open);
    }


    [Fact]
    public async Task ShouldRejectMeasurementAndCloseExceedanceEvents()
    {
        // Arrange
        // Спочатку створюємо вимір вище ліміту, щоб з'явилися перевищення
        var requestCreate = new CreateMeasurementDto(
            Timestamp: DateTime.UtcNow.AddMinutes(-2),
            EmissionSourceId: _emissionSource.Id,
            PollutantId: _pollutant.Id,
            DeviceId: _device.Id,
            UnitId: _mg.Id,
            Period: AveragingWindow.OneHour,
            Value: 50m + 20m
        );

        var createResponse = await Client.PostAsJsonAsync(BaseRoute, requestCreate);
        createResponse.IsSuccessStatusCode.Should().BeTrue();

        var createdDto = await createResponse.ToResponseModel<MeasurementDto>();
        
        var reason = "Sensor malfunction";

        var requestReject = new RejectMeasurementDto(
            Reason: reason
        );

        var route = $"{BaseRoute}/{createdDto.Id}/reject";

        // Act
        var response = await Client.PutAsJsonAsync(route, requestReject);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var dbMeasurement = await Context.Set<Measurement>()
            .AsNoTracking()
            .FirstAsync(x => x.Id.Equals(createdDto.Id));

        dbMeasurement.Status.Should().Be(MeasurementStatus.Rejected);
        dbMeasurement.Reason.Should().Be(reason);

        var exceedances = await Context.Set<ExceedanceEvent>()
            .Where(e => e.MeasurementId.Equals(createdDto.Id))
            .ToListAsync();

        exceedances.Should().NotBeEmpty();
        exceedances.Should().OnlyContain(e => e.Status == ExceedanceEventStatus.Closed);
    }

    [Fact]
    public async Task ShouldReturnNotFoundWhenRejectingNonExistingMeasurement()
    {
        // Arrange
        var id = Guid.NewGuid();
        var route = $"{BaseRoute}/{id}/reject";

        var request = new RejectMeasurementDto(
            Reason: "Does not matter"
        );

        // Act
        var response = await Client.PutAsJsonAsync(route, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MultipleLimitsWithConversion_ShouldCreateCorrectExceedanceEvents()
    {
        // Arrange
        var mg = _mg;
        var measurementValue = 60m;

        var request = new CreateMeasurementDto(
            Timestamp: DateTime.UtcNow,
            EmissionSourceId: _emissionSource.Id,
            PollutantId: _pollutant.Id,
            DeviceId: _device.Id,
            UnitId: mg.Id,
            Period: AveragingWindow.OneHour,
            Value: measurementValue);

        // Act
        var response = await Client.PostAsJsonAsync(BaseRoute, request);


        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var dto = await response.ToResponseModel<MeasurementDto>();

        var exceedances = await Context.Set<ExceedanceEvent>()
            .Include(e => e.Limit)
            .ThenInclude(l => l.Unit)
            .Where(e => e.MeasurementId == dto.Id)
            .ToListAsync();

        exceedances = exceedances
            .OrderBy(e => e.Limit.Unit.ToBaseFactor)
            .ToList();

        exceedances.Should().HaveCount(3);

        //  EXPECTED MAGNITUDES
        //
        // Limit1: mg (factor 1)
        //   measurementInMg = 60 / 1     = 60 mg
        //   magnitude = 60 - 50           = 10 mg

        // Limit2: g (factor 1000)
        //   measurementInG  = 60 / 1000  = 0.06 g
        //   magnitude = 0.06 - 0.05       = 0.01 g

        // Limit3: µg (factor 0.001)
        //   measurementInUg = 60 / 0.001 = 60000 µg
        //   magnitude = 60000 - 50000    = 10000 µg

        var mgEx = exceedances.Single(x => x.Limit.UnitId.Equals(_mg.Id));
        mgEx.Magnitude.Should().BeApproximately(10m, 0.0001m);

        var gEx = exceedances.Single(x => x.Limit.UnitId.Equals(_g.Id));
        gEx.Magnitude.Should().BeApproximately(0.01m, 0.0001m);

        var ugEx = exceedances.Single(x => x.Limit.UnitId.Equals(_ug.Id));
        ugEx.Magnitude.Should().BeApproximately(10000m, 0.0001m);


        exceedances.Should().OnlyContain(e => e.Status == ExceedanceEventStatus.Open);
    }


    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_emissionSource);

        await Context.Set<Pollutant>().AddAsync(_pollutant);
        await Context.Set<MeasureUnit>().AddRangeAsync(_mg, _g, _ug);
        await Context.Set<MonitoringDevice>().AddAsync(_device);


        await Context.Set<MonitoringPlan>().AddAsync(_plan);
        await Context.Set<MonitoringRequirement>().AddAsync(_requirement);

        await Context.Set<Permit>().AddAsync(_multiPermit);
        await Context.Set<EmissionLimit>().AddRangeAsync(_limits3);


        await Context.Set<Measurement>().AddAsync(_seedMeasurement);

        await SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Context.Set<ExceedanceEvent>().RemoveRange(Context.Set<ExceedanceEvent>());
        Context.Set<Measurement>().RemoveRange( Context.Set<Measurement>());
        Context.Set<EmissionLimit>().RemoveRange( Context.Set<EmissionLimit>());
        Context.Set<Permit>().RemoveRange( Context.Set<Permit>());
        Context.Set<MonitoringRequirement>().RemoveRange( Context.Set<MonitoringRequirement>());
        Context.Set<MonitoringPlan>().RemoveRange( Context.Set<MonitoringPlan>());
        Context.Set<MonitoringDevice>().RemoveRange( Context.Set<MonitoringDevice>());
        Context.Set<MeasureUnit>().RemoveRange( Context.Set<MeasureUnit>());
        Context.Set<Pollutant>().RemoveRange( Context.Set<Pollutant>());
        Context.Set<EmissionSource>().RemoveRange( Context.Set<EmissionSource>());
        Context.Set<IedCategory>().RemoveRange(Context.Set<IedCategory>());
        Context.Set<Installation>().RemoveRange( Context.Set<Installation>());
        Context.Set<Site>().RemoveRange( Context.Set<Site>());
        Context.Set<Enterprise>().RemoveRange( Context.Set<Enterprise>());
        Context.Set<Sector>().RemoveRange( Context.Set<Sector>());

        await SaveChangesAsync();
    }
}