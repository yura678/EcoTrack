using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using FluentAssertions;
using Infrastructure.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.EmissionSources;
using Tests.Data.Enterprises;
using Tests.Data.Monitoring;

namespace Api.Tests.Integration.Compliance;

/// <summary>
/// Phase 3.5-B: Verifies the detector emits <see cref="ComplianceEventType.UnenforceableLimit"/>
/// when a limit's unit can't be reconciled with the pollutant's canonical unit — and that the
/// event auto-closes on the next tick once operator fixes the configuration. Also exercises the
/// regression case where a ppm limit against a pollutant with a known molar mass NOW gets
/// compared correctly (was silent-skipped before this phase).
/// </summary>
public class UnenforceableLimitTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private readonly Sector _sector = SectorsData.FirstTestSector();
    private readonly IedCategory _iedCategory = IedCategoriesData.FirstTestIedCategory();
    private readonly MeasureUnit _mg = MeasureUnitsData.MgPerM3();
    private readonly MeasureUnit _kgh = MeasureUnitsData.KgPerHour();

    private readonly Enterprise _enterprise;
    private readonly Site _site;
    private readonly Installation _installation;
    private readonly EmissionSource _source;
    private readonly MonitoringDevice _device;

    private readonly DateTime _lastClosedHourStart;
    private readonly DateTime _lastClosedHourEnd;

    public UnenforceableLimitTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;

        _enterprise = EnterprisesData.FirstTestEquipment(_sector.Id);
        _site = SitesData.FirstTestSite(_enterprise.Id);
        _installation = InstallationData.FirstTestInstallation(_site.Id, _iedCategory.Id);
        _source = EmissionSourcesData.FirstTestEmissionSource(_installation.Id);
        _device = MonitoringDevicesData.FirstTestDevice(_source.Id, _installation.Id);
        BackdateInstall(_device, TimeSpan.FromDays(60));

        var hour = TimeSpan.FromHours(1);
        var now = DateTime.UtcNow;
        _lastClosedHourEnd = new DateTime(now.Ticks - (now.Ticks % hour.Ticks), DateTimeKind.Utc);
        _lastClosedHourStart = _lastClosedHourEnd - hour;
    }

    [Fact]
    public async Task ShouldDetectExceedanceWhenPpmLimitConvertsViaMolarMass()
    {
        // Regression: before Phase 3.5-B the detector compared dimensions for equality; ppm
        // (Dimensionless) vs mg/m³ (MassConcentration) didn't match so the limit was silently
        // skipped. UnitConverter now routes ppm→mg/m³ via molar mass when the pollutant has one.
        // NO₂-like pollutant, M=46 g/mol → 100 ppm = 100×46/22.414 ≈ 205.23 mg/m³ canonical.
        // Measurement 250 mg/m³ > 205.23 → exceedance ratio ≈ 1.2181.
        var ppm = await EnsurePpmUnitAsync();
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id, molarMass: 46m);
        await Context.Set<Pollutant>().AddAsync(pollutant);

        var (permit, limit) = ActivePermitWithLimit(
            value: 100m, unitId: ppm.Id, pollutantId: pollutant.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(
            value: 250m, unitId: _mg.Id, pollutantId: pollutant.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var exceedances = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        exceedances.Should().HaveCount(1, "ppm limit converts to canonical via molar mass " +
            "and 250 mg/m³ exceeds 205.23 mg/m³");
        exceedances[0].Ratio.Should().BeApproximately(1.2181m, 0.001m);

        var unenforceable = await GetEventsAsync(ComplianceEventType.UnenforceableLimit, limit.Id);
        unenforceable.Should().BeEmpty("conversion succeeded — no unenforceable event");
    }

    [Fact]
    public async Task ShouldEmitUnenforceableLimitWhenPpmLimitLacksMolarMass()
    {
        // Same shape as above but pollutant has NO molar mass — UnitConverter can't convert ppm
        // to mass concentration and MassFlow derivation isn't applicable. Detector emits the
        // operator-visible event instead of silently skipping the limit.
        var ppm = await EnsurePpmUnitAsync();
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id, molarMass: null);
        await Context.Set<Pollutant>().AddAsync(pollutant);

        var (permit, limit) = ActivePermitWithLimit(
            value: 100m, unitId: ppm.Id, pollutantId: pollutant.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(
            value: 250m, unitId: _mg.Id, pollutantId: pollutant.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var exceedances = await GetEventsAsync(ComplianceEventType.LimitExceedance, limit.Id);
        exceedances.Should().BeEmpty("conversion failed — exceedance can't be computed");

        var unenforceable = await GetEventsAsync(ComplianceEventType.UnenforceableLimit, limit.Id);
        unenforceable.Should().HaveCount(1);
        unenforceable[0].Status.Should().Be(ComplianceEventStatus.Open);
        unenforceable[0].LimitId.Should().Be(limit.Id);
        unenforceable[0].MeasurementId.Should().BeNull("no single measurement is the cause");
        unenforceable[0].Notes.Should().Contain("no molar mass");
    }

    [Fact]
    public async Task ShouldEmitUnenforceableLimitWhenMassFlowLimitHasNoVolumetricFlow()
    {
        // Concentration measurement + MassFlow limit — derivation needs volumetric flow. Without
        // flow data it can't be derived; previously silent-skip, now emits UnenforceableLimit.
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id);
        await Context.Set<Pollutant>().AddAsync(pollutant);

        var (permit, limit) = ActivePermitWithLimit(
            value: 5m, unitId: _kgh.Id, limitType: LimitType.MassFlow, pollutantId: pollutant.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(
            value: 100m, unitId: _mg.Id, pollutantId: pollutant.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();

        var unenforceable = await GetEventsAsync(ComplianceEventType.UnenforceableLimit, limit.Id);
        unenforceable.Should().HaveCount(1);
        unenforceable[0].Notes.Should().Contain("no volumetric flow");
    }

    [Fact]
    public async Task ShouldNotDuplicateOpenUnenforceableLimit()
    {
        // Second tick with the same misconfiguration must not create a second event.
        var ppm = await EnsurePpmUnitAsync();
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id, molarMass: null);
        await Context.Set<Pollutant>().AddAsync(pollutant);

        var (permit, limit) = ActivePermitWithLimit(
            value: 100m, unitId: ppm.Id, pollutantId: pollutant.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(
            value: 250m, unitId: _mg.Id, pollutantId: pollutant.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();
        await RunDetectionAsync();

        var unenforceable = await GetEventsAsync(ComplianceEventType.UnenforceableLimit, limit.Id);
        unenforceable.Should().HaveCount(1, "dedup by LimitId across ticks");
    }

    [Fact]
    public async Task ShouldAutoCloseUnenforceableLimitAfterOperatorAddsMolarMass()
    {
        // Initial tick: pollutant has no molar mass → event opens.
        // Operator updates pollutant to set MolarMass → next tick must close the event with
        // ResolutionReason.OperatorAction without operator clicking anything.
        var ppm = await EnsurePpmUnitAsync();
        var pollutant = PollutantsData.FirstTestPollutant(_mg.Id, molarMass: null);
        await Context.Set<Pollutant>().AddAsync(pollutant);

        var (permit, limit) = ActivePermitWithLimit(
            value: 100m, unitId: ppm.Id, pollutantId: pollutant.Id);
        await Context.Set<Permit>().AddAsync(permit);
        await Context.Set<EmissionLimit>().AddAsync(limit);

        await Context.Set<Measurement>().AddAsync(HourlyMeasurement(
            value: 50m, unitId: _mg.Id, pollutantId: pollutant.Id));
        await SaveChangesAsync();

        await RunDetectionAsync();
        var firstPass = await GetEventsAsync(ComplianceEventType.UnenforceableLimit, limit.Id);
        firstPass.Should().HaveCount(1);
        firstPass[0].Status.Should().Be(ComplianceEventStatus.Open);

        // Operator updates pollutant with the missing molar mass.
        SetMolarMass(pollutant, 46m);
        Context.Set<Pollutant>().Update(pollutant);
        await SaveChangesAsync();

        await RunDetectionAsync();

        var afterFix = await GetEventsAsync(ComplianceEventType.UnenforceableLimit, limit.Id);
        afterFix.Should().HaveCount(1, "auto-close updates the existing event, not creates a new one");
        afterFix[0].Status.Should().Be(ComplianceEventStatus.Closed);
        afterFix[0].ClosedAt.Should().NotBeNull();
        afterFix[0].ResolutionReason.Should().Be(ResolutionReason.OperatorAction);
        afterFix[0].ResolutionNote.Should().Contain("reconciles");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private (Permit Permit, EmissionLimit Limit) ActivePermitWithLimit(
        decimal value, Guid unitId, Guid pollutantId,
        LimitType limitType = LimitType.Concentration,
        AveragingWindow period = AveragingWindow.Hour1)
    {
        var permitId = Guid.NewGuid();
        var limit = EmissionLimit.New(
            Guid.NewGuid(), value, limitType, period, permitId, unitId, pollutantId,
            emissionSourceId: _source.Id, installationId: null,
            validFrom: DateTime.UtcNow.AddDays(-1), validTo: null);
        var permit = Permit.New(
            permitId, _installation.Id, "P-UEL", PermitType.Air,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddYears(1),
            "Test", null, [limit]);
        permit.ChangeStatus(PermitStatus.Active);
        return (permit, limit);
    }

    private Measurement HourlyMeasurement(decimal value, Guid unitId, Guid pollutantId) =>
        Measurement.New(
            id: Guid.NewGuid(),
            windowStart: _lastClosedHourStart, windowEnd: _lastClosedHourEnd,
            window: AveragingWindow.Hour1, aggregation: Aggregation.Average,
            emissionSourceId: _source.Id, pollutantId: pollutantId,
            deviceId: _device.Id, unitId: unitId,
            value: value, validPointsCount: 60, expectedPointsCount: 60);

    private async Task<MeasureUnit> EnsurePpmUnitAsync()
    {
        // UnitConverter checks Symbol == "ppm" exactly; seed data may already have one.
        var existing = await Context.Set<MeasureUnit>().FirstOrDefaultAsync(u => u.Symbol == "ppm");
        if (existing is not null) return existing;
        var ppm = MeasureUnit.New(Guid.NewGuid(), "ppm", MeasureUnitDimension.Dimensionless, 1m);
        await Context.Set<MeasureUnit>().AddAsync(ppm);
        await SaveChangesAsync();
        return ppm;
    }

    private static void SetMolarMass(Pollutant pollutant, decimal molarMass)
    {
        typeof(Pollutant).GetProperty(nameof(Pollutant.MolarMass))!
            .SetValue(pollutant, molarMass);
    }

    private static void BackdateInstall(MonitoringDevice device, TimeSpan howLongAgo)
    {
        typeof(MonitoringDevice).GetProperty(nameof(MonitoringDevice.InstalledAt))!
            .SetValue(device, DateTime.UtcNow - howLongAgo);
    }

    private async Task<List<ComplianceEvent>> GetEventsAsync(
        ComplianceEventType type, Guid? limitId = null)
    {
        var q = Context.Set<ComplianceEvent>().AsNoTracking()
            .Where(e => e.EventType == type);
        if (limitId.HasValue) q = q.Where(e => e.LimitId == limitId.Value);
        return await q.ToListAsync();
    }

    private async Task RunDetectionAsync()
    {
        await Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('measurement_1m', NULL, NULL);");
        await Context.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('process_parameter_1m', NULL, NULL);");

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ComplianceDetectionService>();
        await service.RunAsync(CancellationToken.None);
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await Context.Set<IedCategory>().AddAsync(_iedCategory);
        await Context.Set<MeasureUnit>().AddRangeAsync(_mg, _kgh);
        await Context.Set<Enterprise>().AddAsync(_enterprise);
        await Context.Set<Site>().AddAsync(_site);
        await Context.Set<Installation>().AddAsync(_installation);
        await Context.Set<EmissionSource>().AddAsync(_source);
        await Context.Set<MonitoringDevice>().AddAsync(_device);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
