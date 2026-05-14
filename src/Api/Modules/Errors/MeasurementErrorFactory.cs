using Application.Features.Measurements.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class MeasurementErrorFactory
{
    public static ObjectResult ToObjectResult(this MeasurementException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                MeasurementNotFoundException
                    or MeasurementRelatedEntityNotFoundException
                    or DuplicateMeasurementException => StatusCodes.Status404NotFound,
                UnhandledMeasurementException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException("Measurement error handler does not implemented.")
            }
        };
    }
}
