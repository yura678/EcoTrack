using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.EmissionSources;
using Tests.Data.Enterprises;
using Tests.Data.Monitoring;

namespace Api.Tests.Integration.Compliance;

public class CurrentViolationProbeTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private readonly Sector _sector;
    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly IedCategory _iedCategory;
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly Pollutant _pollutant;
    private readonly MeasureUnit _mg;
    private readonly MonitoringDevice _device;

    private readonly DateTime _windowEnd;
    private readonly DateTime _windowStart;

    public CurrentViolationProbeTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
        _sector = SectorsData.FirstTestSector();
        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _iedCategory = IedCategoriesData.FirstTestIedCategory();
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _mg = MeasureUnitsData.MgPerM3();
        _pollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);

        var hour = TimeSpan.FromHours(1);
        var now = DateTime.UtcNow;
        _windowEnd = new DateTime(now.Ticks - (now.Ticks % hour.Ticks), DateTimeKind.Utc);
        _windowStart = _windowEnd - hour;
    }

    [Fact]
    public async Task ShouldReportStillViolatingWhenLatestMeasurementStillAboveLimit()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 50m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        // Latest hourly measurement is 80 — well above the 50 limit.
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 80m));

        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id,
            measurementId: null, limit.Id, ratio: 1.6m,
            _windowStart, _windowEnd, notes: "old exceedance");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(true);
    }

    [Fact]
    public async Task ShouldReportNotViolatingWhenLatestMeasurementBackBelowLimit()
    {
        var (permit, limit) = ActivePermitWithLimit(value: 50m);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        // Latest measurement returned to 30 — comfortably below 50.
        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(value: 30m));

        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), _source.Id,
            measurementId: null, limit.Id, ratio: 1.6m,
            _windowStart, _windowEnd, notes: "old exceedance");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(false);
    }

    [Fact]
    public async Task ShouldReportStillViolatingWhenDeviceHasNoRecentData()
    {
        // Device has never reported (no raw_measurement at all) → still offline.
        var ev = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), _source.Id, _device.Id,
            _windowStart, _windowEnd, notes: "offline");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(true);
    }

    [Fact]
    public async Task ShouldReportNotViolatingWhenDeviceJustSentData()
    {
        await Context.Set<RawMeasurement>().AddAsync(RawMeasurement.New(
            DateTime.UtcNow.AddMinutes(-1), _source.Id, _pollutant.Id, _device.Id, _mg.Id, 5m));

        var ev = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), _source.Id, _device.Id,
            _windowStart, _windowEnd, notes: "offline");
        await Context.Set<ComplianceEvent>().AddAsync(ev);
        await SaveChangesAsync();

        var probe = await RunProbeAsync([ev]);
        probe[ev.Id].Should().Be(false);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithLimit(decimal value)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), value, LimitType.Concentration, AveragingWindow.Hour1,
            permitId, _mg.Id, _pollutant.Id,
            emissionSourceId: _source.Id, installationId: null,
            validFrom: DateTime.UtcNow.AddDays(-1), validTo: null);
        var permit = Permit.New(
            permitId, _installation.Id,
            number: "P-PROBE", permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-10),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Test", notes: null,
            emissionLimits: [limit]);
        permit.ChangeStatus(PermitStatus.Active);
        return (permit, limit);
    }

    private Measurement HourlyMeasurement(decimal value) =>
        Measurement.New(
            id: Guid.NewGuid(),
            windowStart: _windowStart, windowEnd: _windowEnd,
            window: AveragingWindow.Hour1, aggregation: Aggregation.Average,
            emissionSourceId: _source.Id, pollutantId: _pollutant.Id,
            deviceId: _device.Id, unitId: _mg.Id,
            value: value, validPointsCount: 60, expectedPointsCount: 60);

    private async Task<IReadOnlyDictionary<Guid, bool?>> RunProbeAsync(
        IReadOnlyCollection<ComplianceEvent> events)
    {
        using var scope = _factory.Services.CreateScope();
        var probe = scope.ServiceProvider.GetRequiredService<ICurrentViolationProbe>();
        return await probe.ProbeAsync(events, CancellationToken.None);
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<Pollutant>().AddAsync(_pollutant);
        await Context.Set<MeasureUnit>().AddAsync(_mg);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
