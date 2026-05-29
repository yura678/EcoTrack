using Domain.Entities.Monitoring;

namespace Application.Common.Interfaces.Queries.Monitoring;

public enum BucketWindow
{
    Minute1 = 0,
    Minute5 = 1,
    Minute15 = 2,
    Minute30 = 3,
    Hour1 = 4,
    Hour6 = 5,
    Day1 = 6,
    // Added to match the frontend AveragingWindow set (Measurements page chart sends
    // the same value that drives compliance materialization). Original BucketWindow
    // values stay at their numeric ids for backward compatibility.
    Minute10 = 7,
    HalfHour = 8,
    Hour24 = 9,
    Month1 = 10,
    Year1 = 11
}

public enum AggregationFunc
{
    Average = 0,
    Max = 1,
    Min = 2,
    Sum = 3,
    P95 = 4
}

public record TimeSeriesPoint(
    DateTime BucketStart,
    decimal Value,
    int TotalPointsCount,
    int ValidPointsCount);

public record HeatmapPoint(
    Guid EmissionSourceId,
    double Latitude,
    double Longitude,
    decimal Value,
    Guid UnitId,
    int TotalPointsCount,
    int ValidPointsCount);

/// <summary>
/// Geographic envelope in WGS84 (SRID 4326), inclusive on all sides. Used to scope a
/// heatmap query to the current map viewport so a tenant-wide call does not pull every
/// emission source on every pan/zoom.
/// </summary>
public record BoundingBox(double MinLongitude, double MinLatitude, double MaxLongitude, double MaxLatitude);

public record ComplianceAuditQueryParams(
    Guid EmissionSourceId,
    Guid PollutantId,
    decimal LimitValue,
    Guid LimitUnitId,
    AveragingWindow Period,
    DateTime From,
    DateTime To);

public record ComplianceAuditResult(
    DateTime From,
    DateTime To,
    AveragingWindow Period,
    decimal LimitValueInBase,
    string LimitUnitSymbol,
    int TotalBuckets,
    long BucketsWithData,
    long ExceedanceBuckets,
    decimal? MaxValueInBase,
    decimal? AvgValueInBase,
    decimal? MaxRatio,
    decimal? ExceedanceRate,
    decimal DataAvailability);

public interface IRawMeasurementQueries
{
    Task<IReadOnlyList<TimeSeriesPoint>> GetTimeSeriesAsync(
        Guid pollutantId,
        Guid emissionSourceId,
        DateTime from,
        DateTime to,
        BucketWindow window,
        AggregationFunc aggregation,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HeatmapPoint>> GetHeatmapAsync(
        Guid pollutantId,
        DateTime from,
        DateTime to,
        AggregationFunc aggregation,
        CancellationToken cancellationToken,
        BoundingBox? bbox = null);

    /// <summary>
    /// Global colour-scale ceiling (98th percentile of per-source values) for the whole
    /// tenant set on (pollutant, from, to, aggregation) — deliberately ignores any viewport
    /// bbox so the heatmap colour scale stays stable while the user pans/zooms. Returns null
    /// when there are no points. Uses the same canonical-unit fold as GetHeatmapAsync.
    /// </summary>
    Task<decimal?> GetHeatmapScaleAsync(
        Guid pollutantId,
        DateTime from,
        DateTime to,
        AggregationFunc aggregation,
        CancellationToken cancellationToken);

    /// <summary>
    /// Read-only "what-if" audit: simulate compliance for a hypothetical limit over a past period
    /// without writing Measurement or ComplianceEvent records.
    /// Returns null when the limit unit doesn't exist or is incompatible with measurement units.
    /// </summary>
    Task<ComplianceAuditResult?> GetComplianceAuditAsync(
        ComplianceAuditQueryParams query,
        CancellationToken cancellationToken);
}
