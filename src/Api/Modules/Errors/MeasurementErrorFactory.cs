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
                    or MonitoringRequirementNotFoundException
                    or DuplicateMeasurementException => StatusCodes.Status404NotFound,
                InvalidAveragingWindowException  => StatusCodes.Status400BadRequest,
                UnhandledMeasurementException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException("Measurement error handler does not implemented.")
            }
        };
    }
}