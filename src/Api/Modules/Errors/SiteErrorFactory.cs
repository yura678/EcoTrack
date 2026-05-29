using Application.Features.Sites.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class SiteErrorFactory
{
    public static ObjectResult ToObjectResult(this SiteException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(SiteException error) => error switch
    {
        SiteNotFoundException or EnterpriseNotFoundException => (StatusCodes.Status404NotFound, null),
        SiteHasDependenciesException => (StatusCodes.Status409Conflict, null),
        UnhandledSiteException => (StatusCodes.Status500InternalServerError, null),
        _ => throw new NotImplementedException(
            $"Site error handler is not implemented for {error.GetType().Name}.")
    };
}
