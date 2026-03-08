using Application.Features.Enterprises.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class EnterpriseErrorFactory
{
    public static ObjectResult ToObjectResult(this EnterpriseException error)
    {
        var statusCode = error switch
        {
            EnterpriseEdrpouAlreadyExistsException
                or EnterpriseHasDependenciesException => StatusCodes.Status409Conflict,
            EnterpriseNotFoundException
                or SectorNotFoundException => StatusCodes.Status404NotFound,
            UnhandledEnterpriseException => StatusCodes.Status500InternalServerError,
            _ => throw new NotImplementedException("Enterprise error handler does not implemented.")
        };

        return new ObjectResult(error.Message)
        {
            StatusCode = statusCode
        };
    }
}