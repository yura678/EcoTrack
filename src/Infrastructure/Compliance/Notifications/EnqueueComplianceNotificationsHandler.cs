using Application.Features.ComplianceEvents.Notifications;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Compliance.Notifications;

/// <summary>
/// Bridge from in-process MediatR notification to Hangfire-backed delivery jobs. Future
/// phases will replace this stub with real channel jobs (email, webhook). Keeping the bridge
/// here means the compliance detector never blocks on SMTP or external HTTP — it simply
/// records an entry in Hangfire's job table and returns.
/// </summary>
public class EnqueueComplianceNotificationsHandler(
    IBackgroundJobClient jobClient,
    ILogger<EnqueueComplianceNotificationsHandler> logger)
    : INotificationHandler<ComplianceEventOpenedNotification>
{
    public Task Handle(ComplianceEventOpenedNotification notification, CancellationToken cancellationToken)
    {
        var jobId = jobClient.Enqueue<ComplianceNotificationDispatcher>(
            dispatcher => dispatcher.DispatchAsync(notification.ComplianceEventId, CancellationToken.None));
        logger.LogInformation(
            "Enqueued compliance notification job {JobId} for event {EventId}",
            jobId, notification.ComplianceEventId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Placeholder Hangfire job. Phases 3-4 will inject the subscription queries and channel
/// adapters here. For now it just logs so the wiring is observable end-to-end in the
/// dashboard.
/// </summary>
public class ComplianceNotificationDispatcher(ILogger<ComplianceNotificationDispatcher> logger)
{
    public Task DispatchAsync(Guid complianceEventId, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Dispatch placeholder fired for compliance event {EventId} — channels not wired yet",
            complianceEventId);
        return Task.CompletedTask;
    }
}
