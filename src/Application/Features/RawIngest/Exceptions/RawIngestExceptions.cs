namespace Application.Features.RawIngest.Exceptions;

public abstract class RawIngestException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public class UnconfiguredDevicePollutantsException(
    Guid deviceId,
    IReadOnlyCollection<Guid> unconfiguredPollutantIds)
    : RawIngestException(
        $"Device '{deviceId}' has no DevicePollutantCapability for pollutant(s): " +
        $"{string.Join(", ", unconfiguredPollutantIds)}.")
{
    public Guid DeviceId { get; } = deviceId;
    public IReadOnlyCollection<Guid> UnconfiguredPollutantIds { get; } = unconfiguredPollutantIds;
}

public class UnhandledRawIngestException(Exception innerException)
    : RawIngestException("Unexpected error occurred while ingesting raw data.", innerException);
