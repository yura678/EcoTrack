using Application.Features.Auth.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class AuthErrorFactory
{
    public static ObjectResult ToObjectResult(this AuthException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                NotAuthenticatedException => StatusCodes.Status401Unauthorized,
                MembershipNotFoundException => StatusCodes.Status403Forbidden,
                UnhandledAuthException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException("Auth error handler not implemented.")
            }
        };
    }
}
