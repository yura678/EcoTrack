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
}
