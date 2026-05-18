using Application.Features.ComplianceEvents.Notifications;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Compliance.Notifications;

/// <summary>
/// Bridge from in-process MediatR notification to a Hangfire-backed delivery job. The
/// compliance detector never blocks on SMTP or external HTTP — it commits the
/// ComplianceEvent and returns; this handler simply records an entry in Hangfire's job
/// table so the dispatcher can pick it up on a worker thread.
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
