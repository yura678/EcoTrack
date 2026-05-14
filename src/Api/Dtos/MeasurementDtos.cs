using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record TimeSeriesQueryDto(
    Guid PollutantId,
    Guid EmissionSourceId,
    DateTime From,
    DateTime To,
    BucketWindow Window,
    AggregationFunc Aggregation = AggregationFunc.Average);

public record HeatmapQueryDto(
    Guid PollutantId,
    DateTime From,
    DateTime To,
    AggregationFunc Aggregation = AggregationFunc.Average);

public record TimeSeriesPointDto(
    DateTime BucketStart,
    decimal Value,
    int TotalPointsCount,
    int ValidPointsCount)
{
    public static TimeSeriesPointDto FromReadModel(TimeSeriesPoint p) =>
        new(p.BucketStart, p.Value, p.TotalPointsCount, p.ValidPointsCount);
}

public record HeatmapPointDto(
    Guid EmissionSourceId,
    double Latitude,
    double Longitude,
    decimal Value,
    Guid UnitId,
    int TotalPointsCount,
    int ValidPointsCount)
{
    public static HeatmapPointDto FromReadModel(HeatmapPoint p) =>
        new(p.EmissionSourceId, p.Latitude, p.Longitude, p.Value, p.UnitId,
            p.TotalPointsCount, p.ValidPointsCount);
}

public record MeasurementQueryDto(
    Guid InstallationId,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20);

public record CreateMeasurementDto(
    DateTime Timestamp,
    Guid EmissionSourceId,
    Guid PollutantId,
    Guid DeviceId,
    Guid UnitId,
    AveragingWindow Window,
    decimal Value);

public record RejectMeasurementDto(
    string Reason);

public record MeasurementDto(
    Guid Id,
    DateTime WindowStart,
    DateTime WindowEnd,
    Guid EmissionSourceId,
    EmissionSourceDto? EmissionSourceDto,
    Guid PollutantId,
    PollutantDto? PollutantDto,
    Guid DeviceId,
    Guid UnitId,
    MeasureUnitDto? MeasureUnitDto,
    AveragingWindow Window,
    Aggregation Aggregation,
    decimal Value,
    decimal? NormalizedValue,
    decimal? Uncertainty,
    int ValidPointsCount,
    int ExpectedPointsCount,
    decimal DataAvailability,
    bool IsRepresentative,
    Quality Quality,
    string? QualityNote,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static MeasurementDto FromDomainModel(Measurement measurement)
    {
        return new MeasurementDto(
            measurement.Id,
            measurement.WindowStart,
            measurement.WindowEnd,
            measurement.EmissionSourceId,
            measurement.EmissionSource is not null
                ? EmissionSourceDto.FromDomainModel(measurement.EmissionSource)
                : null,
            measurement.PollutantId,
            measurement.Pollutant is not null
                ? PollutantDto.FromDomainModel(measurement.Pollutant)
                : null,
            measurement.DeviceId,
            measurement.UnitId,
            measurement.Unit != null
                ? MeasureUnitDto.FromDomainModel(measurement.Unit)
                : null,
            measurement.Window,
            measurement.Aggregation,
            measurement.Value,
            measurement.NormalizedValue,
            measurement.Uncertainty,
            measurement.ValidPointsCount,
            measurement.ExpectedPointsCount,
            measurement.DataAvailability,
            measurement.IsRepresentative,
            measurement.Quality,
            measurement.QualityNote,
            measurement.CreatedAt,
            measurement.UpdatedAt
        );
    }
}
