using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record ComplianceEventListQueryDto(
    ComplianceEventStatus? Status,
    ComplianceEventType? EventType,
    Guid? EmissionSourceId,
    Guid? DeviceId,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20);

public record CloseComplianceEventDto(
    ResolutionReason Reason,
    string? Note);

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
    string? Notes,
    bool? IsCurrentlyViolating,
    ResolutionReason? ResolutionReason,
    string? ResolutionNote,
    Guid? ResolvedByUserId)
{
    public static ComplianceEventDto FromDomainModel(ComplianceEvent ev, bool? isCurrentlyViolating = null)
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
            ev.Notes,
            isCurrentlyViolating,
            ev.ResolutionReason,
            ev.ResolutionNote,
            ev.ResolvedByUserId
        );
    }
}
