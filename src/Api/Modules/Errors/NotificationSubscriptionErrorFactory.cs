using Application.Features.NotificationSubscriptions.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class NotificationSubscriptionErrorFactory
{
    public static ObjectResult ToObjectResult(this NotificationSubscriptionException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(NotificationSubscriptionException error) => error switch
    {
        NotificationSubscriptionNotFoundException => (StatusCodes.Status404NotFound, null),
        NotificationSubscriptionForbiddenException => (StatusCodes.Status403Forbidden, null),
        UnhandledNotificationSubscriptionException => (StatusCodes.Status500InternalServerError, null),
        _ => throw new NotImplementedException(
            $"Notification subscription error handler is not implemented for {error.GetType().Name}.")
    };
}
