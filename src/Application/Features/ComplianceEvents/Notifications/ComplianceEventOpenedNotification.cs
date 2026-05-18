using MediatR;

namespace Application.Features.ComplianceEvents.Notifications;

/// <summary>
/// Raised after a fresh ComplianceEvent has been persisted (status = Open). Handlers fan out
/// to async delivery channels (Hangfire-backed email / webhook senders). Carrying only the Id
/// keeps the in-process notification cheap and reloads the latest state from DB inside each
/// handler — safe against re-publish if the surrounding transaction is retried.
/// </summary>
public record ComplianceEventOpenedNotification(Guid ComplianceEventId) : INotification;
