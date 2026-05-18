using Domain.Entities.Monitoring;
using Domain.Entities.Notifications;

namespace Api.Dtos;

public record CreateNotificationSubscriptionDto(
    NotificationChannel Channel,
    string? Email,
    string? WebhookUrl,
    string? WebhookSecret,
    ComplianceEventType[]? EventTypes,
    Guid[]? EmissionSourceIds);

public record UpdateNotificationSubscriptionDto(
    string? Email,
    string? WebhookUrl,
    string? WebhookSecret,
    ComplianceEventType[]? EventTypes,
    Guid[]? EmissionSourceIds,
    bool IsEnabled);

public record NotificationSubscriptionDto(
    Guid Id,
    Guid UserId,
    NotificationChannel Channel,
    string? Email,
    string? WebhookUrl,
    bool HasWebhookSecret,
    ComplianceEventType[]? EventTypes,
    Guid[]? EmissionSourceIds,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static NotificationSubscriptionDto FromDomainModel(NotificationSubscription s) =>
        new(
            s.Id,
            s.UserId,
            s.Channel,
            s.Email,
            s.WebhookUrl,
            !string.IsNullOrEmpty(s.WebhookSecret),
            s.EventTypes,
            s.EmissionSourceIds,
            s.IsEnabled,
            s.CreatedAt,
            s.UpdatedAt);
}
