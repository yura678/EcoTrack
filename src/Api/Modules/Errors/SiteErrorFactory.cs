using Application.Features.Sites.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class SiteErrorFactory
{
    public static ObjectResult ToObjectResult(this SiteException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                SiteNotFoundException or EnterpriseNotFoundException => StatusCodes.Status404NotFound,
                SiteHasDependenciesException => StatusCodes.Status409Conflict,
                UnhandledSiteException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException("Site error handler does not implemented.")
            }
        };
    }
}