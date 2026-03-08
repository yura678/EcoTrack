using Application.Features.EmissionSources.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class EmissionErrorFactory
{
    public static ObjectResult ToObjectResult(this EmissionSourceException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                EmissionSourceCodeAlreadyExistsException 
                    or EmissionSourceTypeMismatchException => StatusCodes.Status409Conflict,
                EmissionSourceNotFoundException 
                    or InstallationNotFoundException
                    or EmissionSourceHasDependenciesException => StatusCodes.Status404NotFound,
                UnhandledEmissionSourceException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException("Emission error handler does not implemented.")
            }
        };
    }
}