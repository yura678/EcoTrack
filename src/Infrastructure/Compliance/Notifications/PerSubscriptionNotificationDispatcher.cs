using Application.Common.Interfaces.Notifications;
using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Queries.Notifications;
using Domain.Entities.Notifications;
using Hangfire;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Compliance.Notifications;

/// <summary>
/// Hangfire job that delivers one compliance event to one subscription. The fan-out
/// dispatcher (<see cref="ComplianceNotificationDispatcher"/>) enqueues one of these per
/// matching subscription, which gives every recipient its own Hangfire retry budget,
/// independent failure boundary, and audit trail in <see cref="NotificationDelivery"/>.
/// <para>
/// Idempotency comes from the unique <c>(SubscriptionId, ComplianceEventId)</c> index on
/// <see cref="NotificationDelivery"/>: re-runs of the same job find the existing row and
/// short-circuit when it's already <see cref="NotificationDeliveryStatus.Delivered"/>. On
/// failure the row's <c>AttemptCount</c> and <c>LastError</c> are persisted, and the method
/// re-throws so Hangfire's <c>[AutomaticRetry]</c> schedule kicks in.
/// </para>
/// </summary>
public class PerSubscriptionNotificationDispatcher(
    ApplicationDbContext dbContext,
    IComplianceEventQueries complianceEventQueries,
    INotificationSubscriptionQueries subscriptionQueries,
    IEmailComplianceNotificationRenderer emailRenderer,
    IEmailService emailService,
    IWebhookComplianceNotificationPayloadBuilder webhookPayloadBuilder,
    IWebhookSender webhookSender,
    IUnitOfWork unitOfWork,
    ILogger<PerSubscriptionNotificationDispatcher> logger)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 30, 120, 600, 1800, 3600 })]
    public async Task DispatchAsync(
        Guid complianceEventId, Guid subscriptionId, CancellationToken cancellationToken)
    {
        var delivery = await dbContext.Set<NotificationDelivery>()
            .FirstOrDefaultAsync(
                d => d.SubscriptionId == subscriptionId && d.ComplianceEventId == complianceEventId,
                cancellationToken);

        if (delivery is { Status: NotificationDeliveryStatus.Delivered })
        {
            // Hangfire re-ran a job whose delivery already succeeded. This is the idempotency
            // path — without it a transient downstream blip after a successful send could
            // cause double-deliveries on retry.
            return;
        }

        var subscriptionOption = await subscriptionQueries.GetByIdAsync(subscriptionId, cancellationToken);
        var subscription = subscriptionOption.Match(s => s, () => (NotificationSubscription?)null);
        if (subscription is null)
        {
            // Subscription deleted between fan-out and per-sub execution. Nothing to do; we
            // don't dead-letter because the user explicitly removed the channel.
            logger.LogWarning(
                "Subscription {SubId} not found — per-sub dispatch for event {EventId} skipped.",
                subscriptionId, complianceEventId);
            return;
        }

        if (!subscription.IsEnabled)
        {
            logger.LogDebug(
                "Subscription {SubId} disabled — per-sub dispatch for event {EventId} skipped.",
                subscriptionId, complianceEventId);
            return;
        }

        var eventOption = await complianceEventQueries.GetByIdAsync(complianceEventId, cancellationToken);
        var complianceEvent = eventOption.Match(e => e, () => (Domain.Entities.Monitoring.ComplianceEvent?)null);
        if (complianceEvent is null)
        {
            logger.LogWarning(
                "Compliance event {EventId} not found — per-sub dispatch for sub {SubId} skipped.",
                complianceEventId, subscriptionId);
            return;
        }

        if (delivery is null)
        {
            delivery = NotificationDelivery.NewPending(
                Guid.NewGuid(), subscriptionId, complianceEventId, subscription.Channel);
            delivery.AssignTenant(subscription.EnterpriseId);
            await dbContext.Set<NotificationDelivery>().AddAsync(delivery, cancellationToken);
        }

        try
        {
            await SendAsync(complianceEvent, subscription, cancellationToken);
            delivery.MarkDelivered();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Delivered compliance event {EventId} via {Channel} to subscription {SubId} " +
                "(attempt {Attempt}).",
                complianceEventId, subscription.Channel, subscriptionId, delivery.AttemptCount + 1);
        }
        catch (Exception ex)
        {
            delivery.MarkAttempt(Truncate(ex.Message, 2000));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            // Re-throw so Hangfire applies the AutomaticRetry schedule. Once attempts are
            // exhausted the row remains Pending with AttemptCount == 5 and LastError set —
            // operators query notification_delivery for that state to triage. (Auto-flip to
            // DeadLettered via Hangfire filter is intentionally deferred to match the
            // EmailOutboxProcessor precedent.)
            throw;
        }
    }

    private async Task SendAsync(
        Domain.Entities.Monitoring.ComplianceEvent complianceEvent,
        NotificationSubscription subscription,
        CancellationToken cancellationToken)
    {
        switch (subscription.Channel)
        {
            case NotificationChannel.Email when !string.IsNullOrEmpty(subscription.Email):
            {
                var content = emailRenderer.Render(complianceEvent);
                await emailService.SendEmailAsync(
                    subscription.Email, content.Subject, content.Body, cancellationToken);
                break;
            }
            case NotificationChannel.Webhook
                when !string.IsNullOrEmpty(subscription.WebhookUrl)
                  && !string.IsNullOrEmpty(subscription.WebhookSecret):
            {
                var payload = webhookPayloadBuilder.Build(complianceEvent);
                await webhookSender.SendAsync(
                    subscription.WebhookUrl, subscription.WebhookSecret, payload, cancellationToken);
                break;
            }
            default:
                throw new InvalidOperationException(
                    $"Subscription {subscription.Id} has channel {subscription.Channel} but " +
                    "is missing required target fields (Email or WebhookUrl/Secret).");
        }
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max];
}
