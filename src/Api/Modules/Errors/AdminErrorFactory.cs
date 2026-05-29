using Application.Features.Admin.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class AdminErrorFactory
{
    public static ObjectResult ToObjectResult(this AdminException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(AdminException error) => error switch
    {
        RoleNotFoundException => (StatusCodes.Status404NotFound, null),
        UserNotFoundException => (StatusCodes.Status404NotFound, null),
        UserHasNoRolesException => (StatusCodes.Status400BadRequest, null),
        AdminCreationException => (StatusCodes.Status400BadRequest, null),
        InvalidCredentialsException => (StatusCodes.Status401Unauthorized, null),
        UserIsLockedException => (StatusCodes.Status423Locked, null),
        UnhandledAdminException => (StatusCodes.Status500InternalServerError, null),
        _ => throw new NotImplementedException(
            $"Admin error handler is not implemented for {error.GetType().Name}.")
    };
}
