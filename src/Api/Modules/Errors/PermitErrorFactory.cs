using Application.Features.Permits.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class PermitErrorFactory
{
    public static ObjectResult ToObjectResult(this PermitException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(PermitException error) => error switch
    {
        EmissionSourceNotFoundException
            or MeasureUnitNotFoundException
            or PollutantNotFoundException
            or InstallationNotFoundException
            or PermitNotFoundException
            or EmissionLimitNotFoundException => (StatusCodes.Status404NotFound, null),

        PermitNumberAlreadyExistsException => (StatusCodes.Status409Conflict, "Number"),
        // Status-transition errors are action-level — toast only.
        PermitInvalidStatusException
            or ActivePermitAlreadyExistsException
            or CannotActivatePermitOnDecommissionedInstallationException => (StatusCodes.Status409Conflict, null),

        // Date-range error — by convention point at the upper bound (the most common offender
        // when ValidFrom > ValidUntil or duration constraints fail).
        InvalidEmissionLimitDateRangeException => (StatusCodes.Status400BadRequest, "ValidUntil"),
        // Unit dimension mismatch is tied to the selected MeasureUnit.
        IncompatibleLimitUnitDimensionException => (StatusCodes.Status400BadRequest, "MeasureUnitId"),

        UnhandledPermitException => (StatusCodes.Status500InternalServerError, null),

        _ => throw new NotImplementedException(
            $"Permit error handler is not implemented for {error.GetType().Name}.")
    };
}
