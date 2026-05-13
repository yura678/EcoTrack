using Application.Features.DevicePollutantCapabilities.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class DevicePollutantCapabilityErrorFactory
{
    public static ObjectResult ToObjectResult(this DevicePollutantCapabilityException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                CapabilityAlreadyExistsException => StatusCodes.Status409Conflict,
                CapabilityInvalidRangeException => StatusCodes.Status400BadRequest,
                DevicePollutantCapabilityNotFoundException
                    or CapabilityDeviceNotFoundException
                    or CapabilityPollutantNotFoundException
                    or CapabilityMeasureUnitNotFoundException => StatusCodes.Status404NotFound,
                UnhandledDevicePollutantCapabilityException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException(
                    "Device pollutant capability error handler not implemented.")
            }
        };
    }
}
