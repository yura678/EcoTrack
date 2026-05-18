using Application.Common.Interfaces.Notifications;
using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Queries.Notifications;
using Domain.Entities.Notifications;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Compliance.Notifications;

/// <summary>
/// Hangfire job that turns one ComplianceEvent into per-channel deliveries. Loads the event,
/// finds matching subscriptions in the event's enterprise, and dispatches each over its
/// configured channel. Hangfire retries the whole job on exception (default policy);
/// individual channel failures are logged and don't abort the rest.
/// </summary>
public class ComplianceNotificationDispatcher(
    IComplianceEventQueries complianceEventQueries,
    INotificationSubscriptionQueries subscriptionQueries,
    IEmailComplianceNotificationRenderer emailRenderer,
    IEmailService emailService,
    ILogger<ComplianceNotificationDispatcher> logger)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 30, 120, 600, 1800, 3600 })]
    public async Task DispatchAsync(Guid complianceEventId, CancellationToken cancellationToken)
    {
        var eventOption = await complianceEventQueries.GetByIdAsync(complianceEventId, cancellationToken);
        var complianceEvent = eventOption.Match(e => e, () => null!);
        if (complianceEvent is null)
        {
            logger.LogWarning(
                "Compliance event {EventId} not found — dispatcher skipped",
                complianceEventId);
            return;
        }

        var subscriptions = await subscriptionQueries.GetMatchingForEventAsync(
            complianceEvent.EnterpriseId,
            complianceEvent.EventType,
            complianceEvent.EmissionSourceId,
            cancellationToken);

        if (subscriptions.Count == 0)
        {
            logger.LogDebug(
                "No matching subscriptions for event {EventId} ({EventType}/{SourceId})",
                complianceEventId, complianceEvent.EventType, complianceEvent.EmissionSourceId);
            return;
        }

        var sentEmails = 0;
        foreach (var sub in subscriptions)
        {
            if (sub.Channel == NotificationChannel.Email && !string.IsNullOrEmpty(sub.Email))
            {
                try
                {
                    var content = emailRenderer.Render(complianceEvent);
                    await emailService.SendEmailAsync(
                        sub.Email, content.Subject, content.Body, cancellationToken);
                    sentEmails++;
                }
                catch (Exception ex)
                {
                    // Log + continue so one bad recipient doesn't block others. Hangfire's
                    // job-level retry covers transient global failures (e.g. SMTP down) by
                    // re-running the whole dispatch.
                    logger.LogError(ex,
                        "Failed to send email for event {EventId} to subscription {SubId}",
                        complianceEventId, sub.Id);
                }
            }
            // Webhook channel handled in Phase 4.
        }

        logger.LogInformation(
            "Compliance notification dispatched for event {EventId}: {EmailCount} email(s) sent of {Total} matching subscription(s)",
            complianceEventId, sentEmails, subscriptions.Count);
    }
}
