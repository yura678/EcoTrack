using Application.Features.CalibrationRecords.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class CalibrationRecordErrorFactory
{
    public static ObjectResult ToObjectResult(this CalibrationRecordException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(CalibrationRecordException error) => error switch
    {
        // Schedule error usually means the next-due date is wrong (before the performed date,
        // or otherwise inconsistent); point at NextDueAt by convention.
        CalibrationInvalidScheduleException => (StatusCodes.Status400BadRequest, "NextDueAt"),
        CalibrationRecordNotFoundException
            or CalibrationDeviceNotFoundException => (StatusCodes.Status404NotFound, null),
        UnhandledCalibrationRecordException => (StatusCodes.Status500InternalServerError, null),
        _ => throw new NotImplementedException(
            $"Calibration record error handler is not implemented for {error.GetType().Name}.")
    };
}
