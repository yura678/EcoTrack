using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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

namespace Api.Tests.Integration.Ingestion;

public class RawIngestControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly Pollutant _configuredPollutant;
    private readonly Pollutant _unconfiguredPollutant;
    private readonly MeasureUnit _mg;
    private readonly MeasureUnit _g;
    private readonly MonitoringDevice _device;
    private readonly DevicePollutantCapability _capability;

    private readonly byte[] _ingestionSecretBytes = RandomNumberGenerator.GetBytes(32);

    private const string BaseRoute = "api/v1/ingest";

    public RawIngestControllerTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _mg = MeasureUnitsData.MgPerM3();
        _g = MeasureUnitsData.GPerM3();
        _configuredPollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        _unconfiguredPollutant = PollutantsData.SecondTestPollutant(_mg.Id);
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);
        _device.RotateIngestionSecret(Convert.ToBase64String(_ingestionSecretBytes));
        _capability = DevicePollutantCapability.New(
            id: Guid.NewGuid(),
            deviceId: _device.Id,
            pollutantId: _configuredPollutant.Id,
            rangeMin: 0m,
            rangeMax: 500m,
            rangeUnitId: _mg.Id,
            accuracyClass: "Class 2");
    }

    [Fact]
    public async Task ShouldAcceptIngestWhenAllPollutantsAreConfigured()
    {
        var body = new[]
        {
            new RawMeasurementIngestDto(
                Time: DateTime.UtcNow.AddMinutes(-1),
                EmissionSourceId: _source.Id,
                PollutantId: _configuredPollutant.Id,
                UnitId: _mg.Id,
                RawValue: 100m,
                Quality: Quality.Valid)
        };

        var response = await SignAndSendAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var rawCount = await Context.Set<RawMeasurement>()
            .Where(r => r.DeviceId == _device.Id)
            .CountAsync();
        rawCount.Should().Be(1);
    }

    [Fact]
    public async Task ShouldRejectEmptyBatchViaAutoValidation()
    {
        var response = await SignAndSendAsync(Array.Empty<RawMeasurementIngestDto>());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ShouldRejectIngestWhenAnyPollutantLacksCapability()
    {
        var body = new[]
        {
            new RawMeasurementIngestDto(
                Time: DateTime.UtcNow.AddMinutes(-1),
                EmissionSourceId: _source.Id,
                PollutantId: _configuredPollutant.Id,
                UnitId: _mg.Id,
                RawValue: 100m,
                Quality: Quality.Valid),
            new RawMeasurementIngestDto(
                Time: DateTime.UtcNow.AddMinutes(-1),
                EmissionSourceId: _source.Id,
                PollutantId: _unconfiguredPollutant.Id,
                UnitId: _mg.Id,
                RawValue: 100m,
                Quality: Quality.Valid)
        };

        var response = await SignAndSendAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain(_unconfiguredPollutant.Id.ToString());
        responseBody.Should().Contain("DevicePollutantCapability");

        var rawCount = await Context.Set<RawMeasurement>()
            .Where(r => r.DeviceId == _device.Id)
            .CountAsync();
        rawCount.Should().Be(0, "no row may be persisted when the batch is rejected");
    }

    [Fact]
    public async Task ShouldKeepValidQualityWhenRawValueIsInsideCapabilityRange()
    {
        // Capability range 0..500 mg/m³, value 100 mg/m³ → Quality stays Valid.
        var body = new[]
        {
            new RawMeasurementIngestDto(
                Time: DateTime.UtcNow.AddMinutes(-1),
                EmissionSourceId: _source.Id,
                PollutantId: _configuredPollutant.Id,
                UnitId: _mg.Id,
                RawValue: 100m,
                Quality: Quality.Valid)
        };

        var response = await SignAndSendAsync(body);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var row = await Context.Set<RawMeasurement>()
            .Where(r => r.DeviceId == _device.Id)
            .SingleAsync();
        row.Quality.Should().Be(Quality.Valid);
    }

    [Fact]
    public async Task ShouldForceInvalidQualityWhenRawValueOutsideCapabilityRange()
    {
        // Capability range 0..500 mg/m³, value 600 mg/m³ → forced Invalid; row still inserted
        // for audit so downstream detectors can spot the systematic out-of-range condition.
        var body = new[]
        {
            new RawMeasurementIngestDto(
                Time: DateTime.UtcNow.AddMinutes(-1),
                EmissionSourceId: _source.Id,
                PollutantId: _configuredPollutant.Id,
                UnitId: _mg.Id,
                RawValue: 600m,
                Quality: Quality.Valid)
        };

        var response = await SignAndSendAsync(body);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var row = await Context.Set<RawMeasurement>()
            .Where(r => r.DeviceId == _device.Id)
            .SingleAsync();
        row.Quality.Should().Be(Quality.Invalid);
        row.RawValue.Should().Be(600m);
    }

    [Fact]
    public async Task ShouldConvertUnitsBeforeRangeCheck()
    {
        // Capability range is in mg/m³ (factor 1). Measurement is in g/m³ (factor 1000).
        // 0.6 g/m³ = 600 mg/m³ > 500 limit → forced Invalid even though the numeric value
        // alone is well below 500.
        var body = new[]
        {
            new RawMeasurementIngestDto(
                Time: DateTime.UtcNow.AddMinutes(-1),
                EmissionSourceId: _source.Id,
                PollutantId: _configuredPollutant.Id,
                UnitId: _g.Id,
                RawValue: 0.6m,
                Quality: Quality.Valid)
        };

        var response = await SignAndSendAsync(body);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var row = await Context.Set<RawMeasurement>()
            .Where(r => r.DeviceId == _device.Id)
            .SingleAsync();
        row.Quality.Should().Be(Quality.Invalid);
    }

    // ─── HMAC signing helper ─────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SignAndSendAsync<TBody>(TBody body)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(body);
        var timestamp = DateTime.UtcNow.ToString("o");
        var nonce = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"); // 64 chars
        var payload = $"{timestamp}.{nonce}.{json}";
        var signature = Convert.ToBase64String(
            HMACSHA256.HashData(_ingestionSecretBytes, Encoding.UTF8.GetBytes(payload)));

        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseRoute}/measurements")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Remove("Authorization"); // Don't use the TestScheme auth here.
        request.Headers.Add("X-Device-Serial", _device.SerialNumber);
        request.Headers.Add("X-Timestamp", timestamp);
        request.Headers.Add("X-Nonce", nonce);
        request.Headers.Add("X-Signature", signature);
        return await Client.SendAsync(request);
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<Pollutant>().AddRangeAsync(_configuredPollutant, _unconfiguredPollutant);
        await Context.Set<MeasureUnit>().AddRangeAsync(_mg, _g);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await Context.Set<DevicePollutantCapability>().AddAsync(_capability);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
