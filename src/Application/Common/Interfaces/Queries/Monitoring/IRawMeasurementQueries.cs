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
    Day1 = 6
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
