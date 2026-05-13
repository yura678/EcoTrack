using Application.Features.ExceedanceEvents.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class ExceedanceEventErrorFactory
{
    public static ObjectResult ToObjectResult(this ExceedanceEventException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                ExceedanceEventNotFoundException => StatusCodes.Status404NotFound,
                UnhandledExceedanceEventException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException("Exceedance event error handler is not implemented.")
            }
        };
    }
}
