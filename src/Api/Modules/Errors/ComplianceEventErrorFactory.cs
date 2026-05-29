using Application.Features.ComplianceEvents.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class ComplianceEventErrorFactory
{
    public static ObjectResult ToObjectResult(this ComplianceEventException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(ComplianceEventException error) => error switch
    {
        ComplianceEventNotFoundException => (StatusCodes.Status404NotFound, null),
        // Invalid status transition is an action-level error (e.g. trying to close an
        // already-closed event) — toast is the right surface, not a field.
        ComplianceEventInvalidStatusException => (StatusCodes.Status409Conflict, null),
        UnhandledComplianceEventException => (StatusCodes.Status500InternalServerError, null),
        _ => throw new NotImplementedException(
            $"Compliance event error handler is not implemented for {error.GetType().Name}.")
    };
}
