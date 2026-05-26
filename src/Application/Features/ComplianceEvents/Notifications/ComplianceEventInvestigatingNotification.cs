using MediatR;

namespace Application.Features.ComplianceEvents.Notifications;

/// <summary>
/// Raised after an operator transitions a ComplianceEvent to Investigating. Pushes through
/// SignalR so other operators looking at the same dashboard see the status badge flip without
/// a refresh — important because two people often triage in parallel and shouldn't clash on
/// "who owns this incident". Carries only the Id; handlers reload from DB to see the latest.
/// </summary>
public record ComplianceEventInvestigatingNotification(Guid ComplianceEventId) : INotification;
