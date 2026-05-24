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
                or EnterpriseHasDependenciesException
                or EnterpriseAlreadyActiveException
                or EnterpriseInvalidStateForApprovalException
                or EnterpriseInvalidStateForRejectionException => StatusCodes.Status409Conflict,
            EnterpriseRejectionReasonRequiredException => StatusCodes.Status400BadRequest,
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