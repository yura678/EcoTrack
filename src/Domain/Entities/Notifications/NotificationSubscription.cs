using Domain.Common;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using UserEntity = Domain.Entities.User.User;

namespace Domain.Entities.Notifications;

public enum NotificationChannel
{
    Email = 0,
    Webhook = 1
}

/// <summary>
/// Per-user subscription describing how a single user wants to be notified about
/// compliance events. Channel-specific fields (Email vs WebhookUrl/Secret) are mutually
/// exclusive and validated by the factories. Filters are nullable: null means "match
/// everything in this enterprise" for that dimension.
/// </summary>
public class NotificationSubscription : BaseEntity, ITenantOwned
{
    public Guid UserId { get; private set; }
    public UserEntity? User { get; private set; }
    public Guid EnterpriseId { get; private set; }

    public NotificationChannel Channel { get; private set; }

    // Email channel
    public string? Email { get; private set; }

    // Webhook channel
    public string? WebhookUrl { get; private set; }
    public string? WebhookSecret { get; private set; }

    // Filters — null arrays mean "match any" for that dimension.
    public ComplianceEventType[]? EventTypes { get; private set; }
    public Guid[]? EmissionSourceIds { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }

    private NotificationSubscription(
        Guid id, Guid userId, NotificationChannel channel,
        string? email, string? webhookUrl, string? webhookSecret,
        ComplianceEventType[]? eventTypes, Guid[]? emissionSourceIds,
        bool isEnabled, DateTime createdAt)
    {
        Id = id;
        UserId = userId;
        Channel = channel;
        Email = email;
        WebhookUrl = webhookUrl;
        WebhookSecret = webhookSecret;
        EventTypes = eventTypes;
        EmissionSourceIds = emissionSourceIds;
        IsEnabled = isEnabled;
        CreatedAt = createdAt;
    }

    public static NotificationSubscription NewEmail(
        Guid id, Guid userId, string email,
        ComplianceEventType[]? eventTypes = null,
        Guid[]? emissionSourceIds = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required for Email channel.", nameof(email));

        return new NotificationSubscription(
            id, userId, NotificationChannel.Email,
            email: email.Trim(),
            webhookUrl: null, webhookSecret: null,
            eventTypes, emissionSourceIds,
            isEnabled: true, createdAt: DateTime.UtcNow);
    }

    public static NotificationSubscription NewWebhook(
        Guid id, Guid userId, string webhookUrl, string webhookSecret,
        ComplianceEventType[]? eventTypes = null,
        Guid[]? emissionSourceIds = null)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new ArgumentException("WebhookUrl is required for Webhook channel.", nameof(webhookUrl));
        if (string.IsNullOrWhiteSpace(webhookSecret))
            throw new ArgumentException("WebhookSecret is required for Webhook channel.", nameof(webhookSecret));

        return new NotificationSubscription(
            id, userId, NotificationChannel.Webhook,
            email: null,
            webhookUrl: webhookUrl.Trim(), webhookSecret: webhookSecret.Trim(),
            eventTypes, emissionSourceIds,
            isEnabled: true, createdAt: DateTime.UtcNow);
    }

    public void UpdateFilters(
        ComplianceEventType[]? eventTypes,
        Guid[]? emissionSourceIds,
        bool isEnabled)
    {
        EventTypes = eventTypes;
        EmissionSourceIds = emissionSourceIds;
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateEmailTarget(string email)
    {
        if (Channel != NotificationChannel.Email)
            throw new InvalidOperationException(
                "UpdateEmailTarget is only valid for Email-channel subscriptions.");
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        Email = email.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateWebhookTarget(string webhookUrl, string? webhookSecret)
    {
        if (Channel != NotificationChannel.Webhook)
            throw new InvalidOperationException(
                "UpdateWebhookTarget is only valid for Webhook-channel subscriptions.");
        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new ArgumentException("WebhookUrl is required.", nameof(webhookUrl));
        WebhookUrl = webhookUrl.Trim();
        if (!string.IsNullOrWhiteSpace(webhookSecret))
            WebhookSecret = webhookSecret.Trim();
        UpdatedAt = DateTime.UtcNow;
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
                $"EnterpriseId is immutable on NotificationSubscription " +
                $"(current: {EnterpriseId}, attempted: {enterpriseId}).");
        }
    }
}
