using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record ProcessParameterTimeSeriesQueryDto(
    Guid EmissionSourceId,
    ParameterType ParameterType,
    DateTime From,
    DateTime To,
    BucketWindow Window);

public record ProcessParameterLatestQueryDto(
    Guid EmissionSourceId,
    ParameterType[]? ParameterTypes = null);

public record ProcessParameterPointDto(
    DateTime BucketStart,
    decimal? Value,
    Guid UnitId,
    int TotalPointsCount,
    int ValidPointsCount)
{
    public static ProcessParameterPointDto FromReadModel(ProcessParameterPoint p) =>
        new(p.BucketStart, p.Value, p.UnitId, p.TotalPointsCount, p.ValidPointsCount);
}

public record ProcessParameterLatestDto(
    Guid EmissionSourceId,
    ParameterType ParameterType,
    DateTime LastSeenAt,
    decimal Value,
    Guid UnitId)
{
    public static ProcessParameterLatestDto FromReadModel(ProcessParameterLatest l) =>
        new(l.EmissionSourceId, l.ParameterType, l.LastSeenAt, l.Value, l.UnitId);
}
