using Domain.Entities.Monitoring;

namespace Application.Common.Interfaces.Queries.Monitoring;

public record ProcessParameterPoint(
    DateTime BucketStart,
    decimal? Value,
    Guid UnitId,
    int TotalPointsCount,
    int ValidPointsCount);

public record ProcessParameterLatest(
    Guid EmissionSourceId,
    ParameterType ParameterType,
    DateTime LastSeenAt,
    decimal Value,
    Guid UnitId);

public interface IRawProcessParameterQueries
{
    Task<IReadOnlyList<ProcessParameterPoint>> GetTimeSeriesAsync(
        Guid emissionSourceId,
        ParameterType parameterType,
        DateTime from,
        DateTime to,
        BucketWindow window,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProcessParameterLatest>> GetLatestForSourceAsync(
        Guid emissionSourceId,
        IReadOnlyCollection<ParameterType>? parameterTypes,
        CancellationToken cancellationToken);
}
