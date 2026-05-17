using System.Net;
using Api.Dtos;
using Application.Common.Interfaces.Queries.Monitoring;
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

public class ProcessParameterControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector;
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory;
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly MeasureUnit _celsius;
    private readonly MeasureUnit _percent;
    private readonly MonitoringDevice _device;

    private const string BaseRoute = "api/v1/process-parameters";

    public ProcessParameterControllerTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _sector = SectorsData.FirstTestSector();
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _iedCategory = IedCategoriesData.FirstTestIedCategory();
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _celsius = MeasureUnitsData.Celsius();
        _percent = MeasureUnitsData.Percent();
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);
    }

    [Fact]
    public async Task GetTimeSeries_ReturnsBuckets_WhenRawProcessParamsExist()
    {
        await SeedTemperatureSamplesAsync(180m, 190m, 200m);
        await RefreshCaAsync();

        var now = DateTime.UtcNow;
        var from = now.AddHours(-1).ToString("O");
        var to = now.AddMinutes(5).ToString("O");
        var url = $"{BaseRoute}/timeseries?EmissionSourceId={_source.Id}" +
                  $"&ParameterType={(int)ParameterType.StackTemperature}" +
                  $"&From={from}&To={to}&Window={(int)BucketWindow.Minute5}";

        var response = await Client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var points = await response.ToResponseModel<List<ProcessParameterPointDto>>();
        points.Should().NotBeEmpty();
        points.Sum(p => p.TotalPointsCount).Should().Be(3);
        points.Sum(p => p.ValidPointsCount).Should().Be(3);
    }

    [Fact]
    public async Task GetLatest_ReturnsMostRecentPerType()
    {
        var now = DateTime.UtcNow;
        await Context.Set<RawProcessParameter>().AddRangeAsync(
            RawProcessParameter.New(now.AddMinutes(-10), _source.Id, _device.Id,
                ParameterType.StackTemperature, 180m, _celsius.Id),
            RawProcessParameter.New(now.AddMinutes(-1), _source.Id, _device.Id,
                ParameterType.StackTemperature, 195m, _celsius.Id),
            RawProcessParameter.New(now.AddMinutes(-2), _source.Id, _device.Id,
                ParameterType.O2Content, 8.5m, _percent.Id));
        await SaveChangesAsync();

        var response = await Client.GetAsync(
            $"{BaseRoute}/latest?EmissionSourceId={_source.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var latest = await response.ToResponseModel<List<ProcessParameterLatestDto>>();
        latest.Should().HaveCount(2);

        var temp = latest.Single(x => x.ParameterType == ParameterType.StackTemperature);
        temp.Value.Should().Be(195m); // newest temperature wins

        var o2 = latest.Single(x => x.ParameterType == ParameterType.O2Content);
        o2.Value.Should().Be(8.5m);
    }

    [Fact]
    public async Task GetLatest_FiltersByType_WhenProvided()
    {
        var now = DateTime.UtcNow;
        await Context.Set<RawProcessParameter>().AddRangeAsync(
            RawProcessParameter.New(now.AddMinutes(-1), _source.Id, _device.Id,
                ParameterType.StackTemperature, 200m, _celsius.Id),
            RawProcessParameter.New(now.AddMinutes(-1), _source.Id, _device.Id,
                ParameterType.O2Content, 9m, _percent.Id));
        await SaveChangesAsync();

        var response = await Client.GetAsync(
            $"{BaseRoute}/latest?EmissionSourceId={_source.Id}" +
            $"&ParameterTypes={(int)ParameterType.O2Content}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var latest = await response.ToResponseModel<List<ProcessParameterLatestDto>>();
        latest.Should().ContainSingle(x => x.ParameterType == ParameterType.O2Content);
        latest.Should().NotContain(x => x.ParameterType == ParameterType.StackTemperature);
    }

    private async Task SeedTemperatureSamplesAsync(params decimal[] values)
    {
        var now = DateTime.UtcNow;
        var rows = values.Select((value, i) => RawProcessParameter.New(
            time: now.AddMinutes(-2 * (values.Length - i)),
            emissionSourceId: _source.Id,
            deviceId: _device.Id,
            parameterType: ParameterType.StackTemperature,
            value: value,
            unitId: _celsius.Id));
        await Context.Set<RawProcessParameter>().AddRangeAsync(rows);
        await SaveChangesAsync();
    }

    private async Task RefreshCaAsync()
    {
        await Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('process_parameter_1m', NULL, NULL);");
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<MeasureUnit>().AddRangeAsync(_celsius, _percent);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
