using Application.Features.Measurements.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class MeasurementErrorFactory
{
    public static ObjectResult ToObjectResult(this MeasurementException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(MeasurementException error) => error switch
    {
        MeasurementNotFoundException
            or MeasurementRelatedEntityNotFoundException
            or DuplicateMeasurementException => (StatusCodes.Status404NotFound, null),
        UnhandledMeasurementException => (StatusCodes.Status500InternalServerError, null),
        _ => throw new NotImplementedException(
            $"Measurement error handler is not implemented for {error.GetType().Name}.")
    };
}
