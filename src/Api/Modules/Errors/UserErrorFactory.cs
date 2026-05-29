using Application.Features.Users.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class UserErrorFactory
{
    public static ObjectResult ToObjectResult(this UserException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    // Returns the HTTP status to surface and, when the exception is semantically tied to a
    // specific input, the backend property name that names that input. The SPA uses the
    // property name to set a per-field error under the form control instead of only toasting
    // the message.
    private static (int StatusCode, string? FieldName) MapError(UserException error) => error switch
    {
        UserNotFoundException => (StatusCodes.Status404NotFound, null),
        EnterpriseNotFound => (StatusCodes.Status404NotFound, null),
        UserRoleNotFoundException => (StatusCodes.Status404NotFound, null),
        SectorNotFoundException => (StatusCodes.Status404NotFound, "SectorId"),


        UserCreationException => (StatusCodes.Status400BadRequest, null),
        UserVerificationException => (StatusCodes.Status400BadRequest, null),
        UserRoleAssignmentException => (StatusCodes.Status400BadRequest, null),
        InvalidInvitationTokenException => (StatusCodes.Status400BadRequest, null),
        InvitationEmailMismatchException => (StatusCodes.Status400BadRequest, "Email"),

        InvalidCredentialsException => (StatusCodes.Status401Unauthorized, null),
        InvalidRefreshTokenException => (StatusCodes.Status401Unauthorized, null),

        // 403 Forbidden — credentials are valid but tenant is gated by SuperAdmin
        // verification. The body carries the localised message + EDRPOU so the
        // client can show "очікує верифікації" / "відхилено: {reason}".
        EnterprisePendingApprovalException
            or EnterpriseRejectedException => (StatusCodes.Status403Forbidden, null),

        PhoneNumberAlreadyExistsException => (StatusCodes.Status409Conflict, "PhoneNumber"),
        UserNameAlreadyExistsException => (StatusCodes.Status409Conflict, "UserName"),
        EmailAlreadyExistsException => (StatusCodes.Status409Conflict, "Email"),
        EdrpouAlreadyExistsException => (StatusCodes.Status409Conflict, "Edrpou"),
        UserAlreadyHasMembershipException => (StatusCodes.Status409Conflict, null),

        UserIsLockedException => (StatusCodes.Status423Locked, null),


        UnhandledUserException => (StatusCodes.Status500InternalServerError, null),

        _ => throw new NotImplementedException(
            $"User error handler is not implemented for {error.GetType().Name}.")
    };
}
