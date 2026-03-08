using Application.Features.Role.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class RoleErrorFactory
{
    public static ObjectResult ToObjectResult(this RoleException error)
    {
        var statusCode = error switch
        {
            RoleNotFoundException => StatusCodes.Status404NotFound,

            RoleCreationException => StatusCodes.Status400BadRequest,
            RoleClaimsUpdateException => StatusCodes.Status400BadRequest,

            UnhandledRoleException => StatusCodes.Status500InternalServerError,

            _ => throw new NotImplementedException(
                $"Role error handler is not implemented for {error.GetType().Name}.")
        };

        return new ObjectResult(error.Message)
        {
            StatusCode = statusCode
        };
    }
}