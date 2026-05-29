using Application.Features.Installations.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class InstallationErrorFactory
{
    public static ObjectResult ToObjectResult(this InstallationException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(InstallationException error) => error switch
    {
        InstallationNotFoundException
            or IedCategoryNotFoundException
            or SiteNotFoundException => (StatusCodes.Status404NotFound, null),
        InstallationHasDependenciesException => (StatusCodes.Status409Conflict, null),
        UnhandledInstallationException => (StatusCodes.Status500InternalServerError, null),
        _ => throw new NotImplementedException(
            $"Installation error handler is not implemented for {error.GetType().Name}.")
    };
}
