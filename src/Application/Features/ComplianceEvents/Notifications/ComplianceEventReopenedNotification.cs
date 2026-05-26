using MediatR;

namespace Application.Features.ComplianceEvents.Notifications;

/// <summary>
/// Raised when a previously-Closed ComplianceEvent is reopened (status back to Open). Pushed
/// through SignalR so dashboards reactively show the entry returning to the active queue.
/// Carries only the Id; handlers reload from DB to see the final state.
/// </summary>
public record ComplianceEventReopenedNotification(Guid ComplianceEventId) : INotification;
