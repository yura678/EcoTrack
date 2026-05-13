using Application.Features.CalibrationRecords.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class CalibrationRecordErrorFactory
{
    public static ObjectResult ToObjectResult(this CalibrationRecordException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                CalibrationInvalidScheduleException => StatusCodes.Status400BadRequest,
                CalibrationRecordNotFoundException
                    or CalibrationDeviceNotFoundException => StatusCodes.Status404NotFound,
                UnhandledCalibrationRecordException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException(
                    "Calibration record error handler not implemented.")
            }
        };
    }
}
