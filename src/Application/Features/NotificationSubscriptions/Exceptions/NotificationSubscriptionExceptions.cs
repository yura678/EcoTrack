namespace Application.Features.NotificationSubscriptions.Exceptions;

public abstract class NotificationSubscriptionException(
    Guid id, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid Id { get; } = id;
}

public class NotificationSubscriptionNotFoundException(Guid id)
    : NotificationSubscriptionException(id, "Notification subscription not found.");

public class NotificationSubscriptionForbiddenException(Guid id)
    : NotificationSubscriptionException(id,
        "You don't have access to this notification subscription.");

public class UnhandledNotificationSubscriptionException(Guid id, Exception innerException)
    : NotificationSubscriptionException(id,
        "Unexpected error occurred while processing notification subscription.", innerException);
