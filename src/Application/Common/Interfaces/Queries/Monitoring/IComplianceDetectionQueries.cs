using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;

namespace Application.Common.Interfaces.Queries.Monitoring;

// ─── Shared read models for detection + materialization ──────────────────────

public record LimitTarget(
    Guid LimitId,
    Guid EmissionSourceId,
    Guid PollutantId,
    AveragingWindow Period,
    decimal Value,
    Guid UnitId,
    LimitType LimitType,
    Guid? InstallationId);

public record MaterializationTuple(
    Guid SourceId,
    Guid PollutantId,
    AveragingWindow Period);

public record UnitInfo(
    string Symbol,
    MeasureUnitDimension Dimension,
    decimal ToBaseFactor);

public record MeasurementSnapshot(
    Guid Id,
    Guid EmissionSourceId,
    Guid PollutantId,
    decimal Value,
    decimal? NormalizedValue,
    Guid UnitId,
    Quality Quality,
    int ValidPointsCount,
    int ExpectedPointsCount,
    DateTime WindowStart,
    DateTime WindowEnd);

public record OperationalDevice(
    Guid Id,
    Guid EmissionSourceId,
    DateTime? InstalledAt);

public record DeviceCalibrationSnapshot(
    Guid DeviceId,
    Guid EmissionSourceId,
    DateTime? InstalledAt,
    CalibrationResult? LastResult,
    DateTime? LastPerformedAt,
    DateTime? LastNextDueAt);

public record AggregateBucket(
    DateTime WindowStart,
    decimal? Avg,
    long ValidCount,
    long SampleCount,
    Guid UnitId);

public record RollingAverage(
    decimal AvgRate,
    long Samples,
    Guid UnitId);

public record FlowReading(
    decimal Value,
    Guid UnitId);

public record ProcessParamReadings(
    decimal? O2Percent,
    decimal? TemperatureCelsius,
    decimal? PressureKPa,
    decimal? MoisturePercent);

// ─── Query interface ────────────────────────────────────────────────────────

public interface IComplianceDetectionQueries
{
    // ── Limit & source tuples ───────────────────────────────────────────────
    Task<List<LimitTarget>> GetActiveLimitTargetsAsync(
        IReadOnlyCollection<LimitType> limitTypes, CancellationToken ct);

    Task<List<MaterializationTuple>> GetActiveMaterializationTuplesAsync(
        IReadOnlyCollection<LimitType> limitTypes, CancellationToken ct);

    // ── Reference lookups ───────────────────────────────────────────────────
    Task<Dictionary<Guid, UnitInfo>> GetUnitsAsync(
        IReadOnlyCollection<Guid> unitIds, CancellationToken ct);

    Task<Dictionary<Guid, decimal?>> GetPollutantO2ReferencesAsync(
        IReadOnlyCollection<Guid> pollutantIds, CancellationToken ct);

    Task<Dictionary<Guid, Guid>> GetFirstDevicePerSourceAsync(
        IReadOnlyCollection<Guid> sourceIds, CancellationToken ct);

    // ── Measurement reads (detection) ───────────────────────────────────────
    Task<IReadOnlyList<MeasurementSnapshot>> GetMeasurementsForWindowAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        AveragingWindow period,
        DateTime windowEnd,
        CancellationToken ct);

    // ── Measurement materialization ─────────────────────────────────────────
    Task<Dictionary<(Guid SourceId, Guid PollutantId), DateTime?>> GetLastWindowEndsAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        AveragingWindow period,
        CancellationToken ct);

    Task<Dictionary<(Guid SourceId, Guid PollutantId), List<AggregateBucket>>> GetReBucketedBulkAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        TimeSpan period,
        DateTime from,
        DateTime to,
        CancellationToken ct);

    /// <summary>
    /// IED substitution lookup: max value among the last N valid Measurement records for
    /// (source, pollutant, period, Average) strictly before <paramref name="beforeWindowStart"/>.
    /// Returns null if no qualifying history exists.
    /// </summary>
    Task<decimal?> GetMaxValueOverRecentValidWindowsAsync(
        Guid sourceId,
        Guid pollutantId,
        AveragingWindow period,
        DateTime beforeWindowStart,
        int lookbackCount,
        CancellationToken ct);

    // ── Devices & calibration ───────────────────────────────────────────────
    Task<IReadOnlyList<OperationalDevice>> GetOperationalDevicesAsync(CancellationToken ct);

    Task<IReadOnlyList<DeviceCalibrationSnapshot>> GetDevicesWithLatestCalibrationAsync(
        CancellationToken ct);

    Task<Dictionary<Guid, DateTime?>> GetDeviceLastSeenAsync(
        IReadOnlyCollection<Guid> deviceIds, CancellationToken ct);

    // ── Process parameters ──────────────────────────────────────────────────
    Task<Dictionary<(Guid SourceId, DateTime WindowStart), ProcessParamReadings>>
        GetProcessParameterAveragesAsync(
            IReadOnlyCollection<Guid> sourceIds,
            TimeSpan period,
            DateTime from,
            DateTime to,
            CancellationToken ct);

    Task<Dictionary<Guid, FlowReading>> GetVolumetricFlowForRangeAsync(
        IReadOnlyCollection<Guid> sourceIds,
        DateTime from,
        DateTime to,
        CancellationToken ct);

    // ── Raw counts & long-window rolling stats ──────────────────────────────
    Task<Dictionary<(Guid SourceId, Guid PollutantId), long>> GetRawMeasurementCountsAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        DateTime from,
        DateTime to,
        CancellationToken ct);

    Task<Dictionary<(Guid SourceId, Guid PollutantId), RollingAverage>> GetRollingAverageRateAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        DateTime from,
        DateTime to,
        CancellationToken ct);
}
