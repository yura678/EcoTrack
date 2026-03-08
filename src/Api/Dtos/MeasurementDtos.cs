using Domain.Entities.Monitoring;

namespace Api.Dtos;

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
    AveragingWindow Period,
    decimal Value);

public record RejectMeasurementDto(
    string Reason);

public record MeasurementDto(
    Guid Id,
    DateTime Timestamp,
    Guid EmissionSourceId,
    EmissionSourceDto? EmissionSourceDto,
    Guid PollutantId,
    PollutantDto? PollutantDto,
    Guid DeviceId,
    Guid UnitId,
    MeasureUnitDto? MeasureUnitDto,
    AveragingWindow Period,
    decimal Value,
    MeasurementStatus Status,
    string? Reason,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static MeasurementDto FromDomainModel(Measurement measurement)
    {
        return new MeasurementDto(
            measurement.Id,
            measurement.Timestamp,
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
            measurement.Period,
            measurement.Value,
            measurement.Status,
            measurement.Reason,
            measurement.CreatedAt,
            measurement.UpdatedAt
        );
    }
}