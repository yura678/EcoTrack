using Application.Features.Installations.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class InstallationErrorFactory
{
    public static ObjectResult ToObjectResult(this InstallationException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                InstallationNotFoundException
                    or IedCategoryNotFoundException
                    or SiteNotFoundException => StatusCodes.Status404NotFound,
                InstallationHasDependenciesException => StatusCodes.Status409Conflict,
                UnhandledInstallationException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException("Installation error handler does not implemented.")
            }
        };
    }
}