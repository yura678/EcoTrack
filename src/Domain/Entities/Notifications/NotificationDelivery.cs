using Domain.Common;

namespace Domain.Entities.Notifications;

public enum NotificationDeliveryStatus
{
    Pending = 0,
    Delivered = 1,
    DeadLettered = 2
}

/// <summary>
/// Per-(subscription, compliance event) delivery record. One row exists for every recipient
/// that the dispatcher fan-out enqueues a job for; the row is the durable audit answer to
/// "did subscriber X get notified about event Y, and if not, what's the story?". The unique
/// (SubscriptionId, ComplianceEventId) index is also the idempotency key — a Hangfire re-run
/// finds the existing row and short-circuits if it's already Delivered.
/// <para>
/// For the Email channel this row coexists with an <see cref="EmailOutbox"/> row written by
/// <c>IEmailService</c>: the delivery row tracks "we attempted to notify this subscription",
/// the outbox row tracks "this individual email made it through SMTP". For the Webhook
/// channel the delivery row is the only audit (HTTP is direct).
/// </para>
/// </summary>
public class NotificationDelivery : BaseEntity, ITenantOwned
{
    public Guid SubscriptionId { get; private set; }
    public Guid ComplianceEventId { get; private set; }
    public Guid EnterpriseId { get; private set; }

    public NotificationChannel Channel { get; private set; }
    public NotificationDeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? FirstAttemptedAt { get; private set; }
    public DateTime? LastAttemptedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }

    private NotificationDelivery() { }

    public static NotificationDelivery NewPending(
        Guid id, Guid subscriptionId, Guid complianceEventId, NotificationChannel channel) =>
        new()
        {
            Id = id,
            SubscriptionId = subscriptionId,
            ComplianceEventId = complianceEventId,
            Channel = channel,
            Status = NotificationDeliveryStatus.Pending,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };

    public void MarkAttempt(string? error)
    {
        AttemptCount++;
        LastError = error;
        var now = DateTime.UtcNow;
        FirstAttemptedAt ??= now;
        LastAttemptedAt = now;
    }

    public void MarkDelivered()
    {
        Status = NotificationDeliveryStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        LastError = null;
    }

    public void MarkDeadLettered(string finalError)
    {
        Status = NotificationDeliveryStatus.DeadLettered;
        LastError = finalError;
        LastAttemptedAt = DateTime.UtcNow;
    }

    public void AssignTenant(Guid enterpriseId)
    {
        if (EnterpriseId == Guid.Empty)
        {
            EnterpriseId = enterpriseId;
        }
        else if (EnterpriseId != enterpriseId)
        {
            throw new InvalidOperationException(
                $"EnterpriseId is immutable on NotificationDelivery " +
                $"(current: {EnterpriseId}, attempted: {enterpriseId}).");
        }
    }
}
