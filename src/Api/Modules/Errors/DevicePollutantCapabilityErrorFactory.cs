using Application.Features.DevicePollutantCapabilities.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class DevicePollutantCapabilityErrorFactory
{
    public static ObjectResult ToObjectResult(this DevicePollutantCapabilityException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(DevicePollutantCapabilityException error) => error switch
    {
        // (DeviceId, PollutantId) uniqueness — the form lets the user choose a pollutant for
        // the device, so surface the conflict under that selector.
        CapabilityAlreadyExistsException => (StatusCodes.Status409Conflict, "PollutantId"),
        // Min/Max range — point at the upper bound by convention.
        CapabilityInvalidRangeException => (StatusCodes.Status400BadRequest, "MaxValue"),
        DevicePollutantCapabilityNotFoundException
            or CapabilityDeviceNotFoundException
            or CapabilityPollutantNotFoundException
            or CapabilityMeasureUnitNotFoundException => (StatusCodes.Status404NotFound, null),
        UnhandledDevicePollutantCapabilityException => (StatusCodes.Status500InternalServerError, null),
        _ => throw new NotImplementedException(
            $"Device pollutant capability error handler is not implemented for {error.GetType().Name}.")
    };
}
