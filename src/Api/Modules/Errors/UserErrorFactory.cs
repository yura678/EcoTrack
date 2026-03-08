using Application.Features.Users.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class UserErrorFactory
{
    public static ObjectResult ToObjectResult(this UserException error)
    {
        var statusCode = error switch
        {
            UserNotFoundException => StatusCodes.Status404NotFound,
            EnterpriseNotFound => StatusCodes.Status404NotFound,
            UserRoleNotFoundException => StatusCodes.Status404NotFound,
            

            UserCreationException => StatusCodes.Status400BadRequest,
            UserVerificationException => StatusCodes.Status400BadRequest,
            UserRoleAssignmentException => StatusCodes.Status400BadRequest,
            InvalidInvitationTokenException => StatusCodes.Status400BadRequest,

            InvalidCredentialsException => StatusCodes.Status401Unauthorized,
            InvalidRefreshTokenException => StatusCodes.Status401Unauthorized,

            PhoneNumberAlreadyExistsException => StatusCodes.Status409Conflict,
            UserNameAlreadyExistsException => StatusCodes.Status409Conflict,
            EmailAlreadyExistsException => StatusCodes.Status409Conflict,
            
            UserIsLockedException => StatusCodes.Status423Locked,


            UnhandledUserException => StatusCodes.Status500InternalServerError,

            _ => throw new NotImplementedException(
                $"User error handler is not implemented for {error.GetType().Name}.")
        };


        return new ObjectResult(error.Message)
        {
            StatusCode = statusCode
        };
    }
}