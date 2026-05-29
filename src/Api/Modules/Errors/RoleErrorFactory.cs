using Application.Features.Role.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class RoleErrorFactory
{
    public static ObjectResult ToObjectResult(this RoleException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(RoleException error) => error switch
    {
        RoleNotFoundException => (StatusCodes.Status404NotFound, null),
        RoleCreationException => (StatusCodes.Status400BadRequest, null),
        RoleClaimsUpdateException => (StatusCodes.Status400BadRequest, null),
        RoleInUseException => (StatusCodes.Status409Conflict, null),
        UnhandledRoleException => (StatusCodes.Status500InternalServerError, null),
        _ => throw new NotImplementedException(
            $"Role error handler is not implemented for {error.GetType().Name}.")
    };
}
