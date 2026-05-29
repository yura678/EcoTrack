using Application.Features.Auth.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class AuthErrorFactory
{
    public static ObjectResult ToObjectResult(this AuthException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(AuthException error) => error switch
    {
        NotAuthenticatedException => (StatusCodes.Status401Unauthorized, null),
        MembershipNotFoundException => (StatusCodes.Status403Forbidden, null),
        UnhandledAuthException => (StatusCodes.Status500InternalServerError, null),
        _ => throw new NotImplementedException(
            $"Auth error handler is not implemented for {error.GetType().Name}.")
    };
}
