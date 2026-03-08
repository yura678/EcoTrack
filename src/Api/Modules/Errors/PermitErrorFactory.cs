using Application.Features.Permits.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class PermitErrorFactory
{
    public static ObjectResult ToObjectResult(this PermitException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                EmissionSourceNotFoundException
                    or MeasureUnitNotFoundException
                    or PollutantNotFoundException
                    or InstallationNotFoundException
                    or PermitNotFoundException
                    or EmissionLimitNotFoundException => StatusCodes.Status404NotFound,
                ActivePermitAlreadyExistsException
                    or PermitInvalidStatusException
                    or PermitNumberAlreadyExistsException => StatusCodes.Status409Conflict,
                InvalidEmissionLimitDateRangeException => StatusCodes.Status400BadRequest,
                UnhandledPermitException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException("Permit error handler does not implemented.")
            }
        };
    }
}