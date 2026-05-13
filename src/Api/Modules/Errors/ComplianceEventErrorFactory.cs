using Application.Features.ComplianceEvents.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class ComplianceEventErrorFactory
{
    public static ObjectResult ToObjectResult(this ComplianceEventException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                ComplianceEventNotFoundException => StatusCodes.Status404NotFound,
                UnhandledComplianceEventException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException("Compliance event error handler is not implemented.")
            }
        };
    }
}
