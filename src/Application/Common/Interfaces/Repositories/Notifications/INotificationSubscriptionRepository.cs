using Domain.Entities.Notifications;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Notifications;

public interface INotificationSubscriptionRepository
{
    Task<Option<NotificationSubscription>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<NotificationSubscription> AddAsync(NotificationSubscription entity, CancellationToken cancellationToken);
    NotificationSubscription Update(NotificationSubscription entity);
    NotificationSubscription Delete(NotificationSubscription entity);
}
