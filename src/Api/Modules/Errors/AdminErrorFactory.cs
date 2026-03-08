using Application.Features.Admin.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class AdminErrorFactory
{
    public static ObjectResult ToObjectResult(this AdminException error)
    {
        var statusCode = error switch
        {
            RoleNotFoundException => StatusCodes.Status404NotFound,
            UserNotFoundException => StatusCodes.Status404NotFound,

            UserHasNoRolesException => StatusCodes.Status400BadRequest,
            AdminCreationException => StatusCodes.Status400BadRequest,

            InvalidCredentialsException => StatusCodes.Status401Unauthorized,

            UserIsLockedException => StatusCodes.Status423Locked,

            UnhandledAdminException => StatusCodes.Status500InternalServerError,

            _ => throw new NotImplementedException(
                $"Admin error handler is not implemented for {error.GetType().Name}.")
        };
        return new ObjectResult(error.Message)
        {
            StatusCode = statusCode
        };
    }
}