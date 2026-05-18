using Domain.Entities.Monitoring;
using Domain.Entities.Notifications;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Notifications;

public interface INotificationSubscriptionQueries
{
    Task<Option<NotificationSubscription>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// All subscriptions owned by a user inside the current tenant.
    /// </summary>
    Task<IReadOnlyList<NotificationSubscription>> GetByUserAsync(
        Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// All enabled subscriptions for the enterprise whose filters match the given event,
    /// used by notification dispatchers to find recipients without loading every row.
    /// </summary>
    Task<IReadOnlyList<NotificationSubscription>> GetMatchingForEventAsync(
        Guid enterpriseId,
        ComplianceEventType eventType,
        Guid emissionSourceId,
        CancellationToken cancellationToken);
}
