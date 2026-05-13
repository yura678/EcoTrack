using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record ComplianceEventDto(
    Guid Id,
    ComplianceEventType EventType,
    Guid EmissionSourceId,
    Guid? MeasurementId,
    Guid? LimitId,
    Guid? DeviceId,
    DateTime WindowStart,
    DateTime WindowEnd,
    decimal? Ratio,
    ComplianceEventStatus Status,
    DateTime DetectedAt,
    DateTime? ClosedAt,
    DateTime? UpdatedAt,
    string? Notes)
{
    public static ComplianceEventDto FromDomainModel(ComplianceEvent ev)
    {
        return new ComplianceEventDto(
            ev.Id,
            ev.EventType,
            ev.EmissionSourceId,
            ev.MeasurementId,
            ev.LimitId,
            ev.DeviceId,
            ev.WindowStart,
            ev.WindowEnd,
            ev.Ratio,
            ev.Status,
            ev.DetectedAt,
            ev.ClosedAt,
            ev.UpdatedAt,
            ev.Notes
        );
    }
}
