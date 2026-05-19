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
    AveragingWindow Period,
    DateTime EarliestValidFrom);

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
    DateTime WindowEnd,
    AveragingWindow Window);

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

/// <summary>
/// Per-(source, device, pollutant) summary of raw_measurement quality over a rolling window.
/// Returned only when the invalid ratio exceeds the configured threshold and total samples
/// meet the minimum count — used to open OutOfRangeReading events.
/// </summary>
public record OutOfRangeWindow(
    Guid SourceId,
    Guid DeviceId,
    Guid PollutantId,
    long Total,
    long InvalidCount,
    decimal InvalidRatio);

// ─── Query interface ────────────────────────────────────────────────────────

public interface IComplianceDetectionQueries
{
    // ── Limit & source tuples ───────────────────────────────────────────────
    Task<List<LimitTarget>> GetActiveLimitTargetsAsync(
        IReadOnlyCollection<LimitType> limitTypes, CancellationToken ct);

    /// <summary>
    /// Loads currently-active limits by Id (validity + permit state checked) — used by the
    /// current-violation probe to recover the reference data for an open ComplianceEvent.
    /// </summary>
    Task<Dictionary<Guid, LimitTarget>> GetActiveLimitsByIdsAsync(
        IReadOnlyCollection<Guid> limitIds, CancellationToken ct);

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

    /// <summary>
    /// Returns every Measurement whose WindowEnd falls in [fromWindowEndInclusive, toWindowEndInclusive].
    /// Used by the LimitExceedance detector to re-evaluate windows that may have been mutated in
    /// place by late-arriving raw data — the per-tick "rescan" symmetric to the materializer's
    /// LateArrivingRescanWindows knob.
    /// </summary>
    Task<IReadOnlyList<MeasurementSnapshot>> GetMeasurementsForWindowRangeAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        AveragingWindow period,
        DateTime fromWindowEndInclusive,
        DateTime toWindowEndInclusive,
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

    /// <summary>
    /// Single rolling-window O2 average per source (not bucketed). Used for IED normalization
    /// of long-window concentration averages (AnnualLoad).
    /// </summary>
    Task<Dictionary<Guid, decimal>> GetO2AverageForRangeAsync(
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

    /// <summary>
    /// Total raw_measurement count per source over a time range — used to probe whether a
    /// MissingMeasurement event is still "source is silent" right now.
    /// </summary>
    Task<Dictionary<Guid, long>> GetRawMeasurementCountsBySourceAsync(
        IReadOnlyCollection<Guid> sourceIds,
        DateTime from,
        DateTime to,
        CancellationToken ct);

    /// <summary>
    /// Latest Measurement per (sourceId, pollutantId) with the given period/Average — used by
    /// the current-violation probe to compare the freshest aggregate against a limit.
    /// </summary>
    Task<IReadOnlyList<MeasurementSnapshot>> GetLatestMeasurementsAsync(
        IReadOnlyCollection<(Guid SourceId, Guid PollutantId)> pairs,
        AveragingWindow period,
        CancellationToken ct);

    /// <summary>
    /// Loads MeasurementSnapshot records by Id — used by the DataAvailabilityLoss probe to
    /// recover the (source, pollutant, window) tuple from the event's original Measurement.
    /// </summary>
    Task<IReadOnlyList<MeasurementSnapshot>> GetMeasurementsByIdsAsync(
        IReadOnlyCollection<Guid> measurementIds, CancellationToken ct);

    Task<Dictionary<(Guid SourceId, Guid PollutantId), RollingAverage>> GetRollingAverageRateAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        DateTime from,
        DateTime to,
        CancellationToken ct);

    /// <summary>
    /// Scans raw_measurement and returns one row per (source, device, pollutant) whose share of
    /// Quality=Invalid rows over the window exceeds <paramref name="threshold"/> AND whose total
    /// row count is at least <paramref name="minSampleCount"/>.
    /// </summary>
    Task<IReadOnlyList<OutOfRangeWindow>> GetOutOfRangeWindowsAsync(
        DateTime from,
        DateTime to,
        decimal threshold,
        int minSampleCount,
        CancellationToken ct);
}
