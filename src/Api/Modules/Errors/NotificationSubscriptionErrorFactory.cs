using Application.Features.NotificationSubscriptions.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class NotificationSubscriptionErrorFactory
{
    public static ObjectResult ToObjectResult(this NotificationSubscriptionException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                NotificationSubscriptionNotFoundException => StatusCodes.Status404NotFound,
                NotificationSubscriptionForbiddenException => StatusCodes.Status403Forbidden,
                UnhandledNotificationSubscriptionException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException(
                    "NotificationSubscription error handler is not implemented.")
            }
        };
    }
}
