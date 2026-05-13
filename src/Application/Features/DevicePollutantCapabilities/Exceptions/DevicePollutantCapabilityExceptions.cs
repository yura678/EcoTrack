namespace Application.Features.DevicePollutantCapabilities.Exceptions;

public abstract class DevicePollutantCapabilityException(
    Guid id,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid Id { get; } = id;
}

public class DevicePollutantCapabilityNotFoundException(Guid id)
    : DevicePollutantCapabilityException(id, $"Device pollutant capability with ID '{id}' was not found.");

public class CapabilityAlreadyExistsException(Guid deviceId, Guid pollutantId)
    : DevicePollutantCapabilityException(Guid.Empty,
        $"Device '{deviceId}' already has a capability for pollutant '{pollutantId}'.");

public class CapabilityInvalidRangeException(decimal min, decimal max)
    : DevicePollutantCapabilityException(Guid.Empty,
        $"Invalid range: RangeMin ({min}) must be less than RangeMax ({max}).");

public class CapabilityDeviceNotFoundException(Guid deviceId)
    : DevicePollutantCapabilityException(Guid.Empty,
        $"Device with ID '{deviceId}' was not found.");

public class CapabilityPollutantNotFoundException(Guid pollutantId)
    : DevicePollutantCapabilityException(Guid.Empty,
        $"Pollutant with ID '{pollutantId}' was not found.");

public class CapabilityMeasureUnitNotFoundException(Guid unitId)
    : DevicePollutantCapabilityException(Guid.Empty,
        $"Measure unit with ID '{unitId}' was not found.");

public class UnhandledDevicePollutantCapabilityException(Guid id, Exception? innerException = null)
    : DevicePollutantCapabilityException(id, "Unexpected error occurred.", innerException);
