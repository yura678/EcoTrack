using Application.Common.Interfaces.Notifications;
using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Queries.Notifications;
using Domain.Entities.Monitoring;
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
    IWebhookComplianceNotificationPayloadBuilder webhookPayloadBuilder,
    IWebhookSender webhookSender,
    IUnitOfWork unitOfWork,
    ILogger<ComplianceNotificationDispatcher> logger)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 30, 120, 600, 1800, 3600 })]
    public async Task DispatchAsync(Guid complianceEventId, CancellationToken cancellationToken)
    {
        var eventOption = await complianceEventQueries.GetByIdAsync(complianceEventId, cancellationToken);
        await eventOption.Match(
            Some: ev => DispatchForEventAsync(ev, cancellationToken),
            None: () =>
            {
                logger.LogWarning(
                    "Compliance event {EventId} not found — dispatcher skipped",
                    complianceEventId);
                return Task.CompletedTask;
            });
    }

    private async Task DispatchForEventAsync(ComplianceEvent complianceEvent, CancellationToken cancellationToken)
    {
        var subscriptions = await subscriptionQueries.GetMatchingForEventAsync(
            complianceEvent.EnterpriseId,
            complianceEvent.EventType,
            complianceEvent.EmissionSourceId,
            cancellationToken);

        if (subscriptions.Count == 0)
        {
            logger.LogDebug(
                "No matching subscriptions for event {EventId} ({EventType}/{SourceId})",
                complianceEvent.Id, complianceEvent.EventType, complianceEvent.EmissionSourceId);
            return;
        }

        // Build the webhook payload lazily — most batches will have 0 webhook subs and we
        // don't want to pay the serialization cost when nothing consumes it.
        string? webhookPayload = null;

        var sentEmails = 0;
        var sentWebhooks = 0;
        foreach (var sub in subscriptions)
        {
            try
            {
                switch (sub.Channel)
                {
                    case NotificationChannel.Email when !string.IsNullOrEmpty(sub.Email):
                    {
                        var content = emailRenderer.Render(complianceEvent);
                        await emailService.SendEmailAsync(
                            sub.Email, content.Subject, content.Body, cancellationToken);
                        sentEmails++;
                        break;
                    }
                    case NotificationChannel.Webhook
                        when !string.IsNullOrEmpty(sub.WebhookUrl)
                          && !string.IsNullOrEmpty(sub.WebhookSecret):
                    {
                        webhookPayload ??= webhookPayloadBuilder.Build(complianceEvent);
                        await webhookSender.SendAsync(
                            sub.WebhookUrl, sub.WebhookSecret, webhookPayload, cancellationToken);
                        sentWebhooks++;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Per-recipient isolation: one bad URL or SMTP miss doesn't take out the
                // dispatch for everyone else. Hangfire's job-level retry handles transient
                // global failures (e.g. whole SMTP down) by re-running this method.
                logger.LogError(ex,
                    "Failed to deliver compliance event {EventId} via {Channel} to subscription {SubId}",
                    complianceEvent.Id, sub.Channel, sub.Id);
            }
        }

        // Commit every email_outbox row staged in the loop in one shot. The webhook channel
        // doesn't touch the DbContext (direct HTTP) so this only flushes email rows. Safe to
        // call when sentEmails == 0 — SaveChanges with no pending changes is a no-op.
        if (sentEmails > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Compliance notification dispatched for event {EventId}: " +
            "{EmailCount} email(s), {WebhookCount} webhook(s) of {Total} matching subscription(s)",
            complianceEvent.Id, sentEmails, sentWebhooks, subscriptions.Count);
    }
}
