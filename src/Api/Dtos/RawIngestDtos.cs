using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record RawMeasurementIngestDto(
    DateTime Time,
    Guid EmissionSourceId,
    Guid PollutantId,
    Guid UnitId,
    decimal RawValue,
    Quality Quality);

public record RawProcessParameterIngestDto(
    DateTime Time,
    Guid EmissionSourceId,
    ParameterType ParameterType,
    decimal Value,
    Guid UnitId,
    Quality Quality);

public record IngestBatchResult(int InsertedCount);
