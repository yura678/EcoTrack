using Application.Features.Enterprises.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class EnterpriseErrorFactory
{
    public static ObjectResult ToObjectResult(this EnterpriseException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(EnterpriseException error) => error switch
    {
        EnterpriseEdrpouAlreadyExistsException => (StatusCodes.Status409Conflict, "Edrpou"),
        EnterpriseHasDependenciesException
            or EnterpriseAlreadyActiveException
            or EnterpriseInvalidStateForApprovalException
            or EnterpriseInvalidStateForRejectionException => (StatusCodes.Status409Conflict, null),
        // Rejection requires a Reason from the moderator's dialog — surface there.
        EnterpriseRejectionReasonRequiredException => (StatusCodes.Status400BadRequest, "Reason"),
        EnterpriseNotFoundException
            or SectorNotFoundException => (StatusCodes.Status404NotFound, null),
        UnhandledEnterpriseException => (StatusCodes.Status500InternalServerError, null),
        _ => throw new NotImplementedException(
            $"Enterprise error handler is not implemented for {error.GetType().Name}.")
    };
}
