using MediatR;

namespace Application.Features.ComplianceEvents.Notifications;

/// <summary>
/// Raised after a ComplianceEvent transitions to Closed (auto-close by the detector or manual
/// close by an operator). Handlers fan out to in-process SignalR so dashboards reactively
/// reflect the new state without polling. Carries only the Id; handlers reload from DB to see
/// the final status + resolution metadata.
/// </summary>
public record ComplianceEventClosedNotification(Guid ComplianceEventId) : INotification;
