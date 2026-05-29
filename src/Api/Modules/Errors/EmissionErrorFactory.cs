using Application.Features.EmissionSources.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class EmissionErrorFactory
{
    public static ObjectResult ToObjectResult(this EmissionSourceException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(EmissionSourceException error) => error switch
    {
        EmissionSourceCodeAlreadyExistsException => (StatusCodes.Status409Conflict, "Code"),
        // Type mismatch is a routing/identity issue, not a form input — toast only.
        EmissionSourceTypeMismatchException => (StatusCodes.Status409Conflict, null),

        EmissionSourceNotFoundException
            or InstallationNotFoundException
            or EmissionSourceHasDependenciesException => (StatusCodes.Status404NotFound, null),

        UnhandledEmissionSourceException => (StatusCodes.Status500InternalServerError, null),

        _ => throw new NotImplementedException(
            $"Emission source error handler is not implemented for {error.GetType().Name}.")
    };
}
