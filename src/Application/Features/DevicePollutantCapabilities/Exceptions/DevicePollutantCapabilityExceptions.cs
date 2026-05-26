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
    : DevicePollutantCapabilityException(id, "Device pollutant configuration not found.");

public class CapabilityAlreadyExistsException(Guid deviceId, Guid pollutantId)
    : DevicePollutantCapabilityException(Guid.Empty,
        "This device is already configured for this pollutant.")
{
    public Guid DeviceId { get; } = deviceId;
    public Guid PollutantId { get; } = pollutantId;
}

public class CapabilityInvalidRangeException(decimal min, decimal max)
    : DevicePollutantCapabilityException(Guid.Empty,
        $"Invalid range: minimum ({min}) must be less than maximum ({max}).")
{
    public decimal Min { get; } = min;
    public decimal Max { get; } = max;
}

public class CapabilityDeviceNotFoundException(Guid deviceId)
    : DevicePollutantCapabilityException(Guid.Empty, "Device not found.")
{
    public Guid DeviceId { get; } = deviceId;
}

public class CapabilityPollutantNotFoundException(Guid pollutantId)
    : DevicePollutantCapabilityException(Guid.Empty, "Pollutant not found.")
{
    public Guid PollutantId { get; } = pollutantId;
}

public class CapabilityMeasureUnitNotFoundException(Guid unitId)
    : DevicePollutantCapabilityException(Guid.Empty, "Measurement unit not found.")
{
    public Guid UnitId { get; } = unitId;
}

public class UnhandledDevicePollutantCapabilityException(Guid id, Exception? innerException = null)
    : DevicePollutantCapabilityException(id, "Unexpected error occurred.", innerException);
