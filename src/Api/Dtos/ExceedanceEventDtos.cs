using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record ExceedanceEventDto(
    Guid Id,
    Guid MeasurementId,
    MeasurementDto? MeasurementDto,
    Guid EmissionLimitId,
    EmissionLimitDto? LimitDto,
    decimal Magnitude,
    ExceedanceEventStatus Status,
    DateTime DetectedAt,
    DateTime? UpdatedAt,
    string? Notes)
{
    public static ExceedanceEventDto FromDomainModel(ExceedanceEvent exceedanceEvent)
    {
        return new ExceedanceEventDto(
            exceedanceEvent.Id,
            exceedanceEvent.MeasurementId,
            exceedanceEvent.Measurement is not null
                ? MeasurementDto.FromDomainModel(exceedanceEvent.Measurement)
                : null,
            exceedanceEvent.LimitId,
            exceedanceEvent.Limit is not null
                ? EmissionLimitDto.FromDomainModel(exceedanceEvent.Limit)
                : null,
            exceedanceEvent.Magnitude,
            exceedanceEvent.Status,
            exceedanceEvent.DetectedAt,
            exceedanceEvent.UpdatedAt,
            exceedanceEvent.Notes
        );
    }
}