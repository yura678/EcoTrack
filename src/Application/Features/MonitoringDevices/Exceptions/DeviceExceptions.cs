namespace Application.Features.MonitoringDevices.Exceptions;

public abstract class MonitoringDeviceException(
    Guid monitoringDeviceId,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid MonitoringDeviceId { get; } = monitoringDeviceId;
}

public class MonitoringDeviceNumberAlreadyExistsException(
    Guid monitoringDeviceId,
    string serialNumber)
    : MonitoringDeviceException(monitoringDeviceId,
        $"A monitoring device with number '{serialNumber}' already exists.");

public class EmissionSourceNotFoundException(
    Guid monitoringDeviceId,
    Guid emissionSourceId)
    : MonitoringDeviceException(monitoringDeviceId, $"Emission source with ID '{emissionSourceId}' was not found.");

public class InstallationNotFoundException(
    Guid monitoringDeviceId,
    Guid installationId)
    : MonitoringDeviceException(monitoringDeviceId, $"Installation with ID '{installationId}' was not found.");

public class MonitoringDeviceNotFoundException(
    Guid monitoringDeviceId)
    : MonitoringDeviceException(monitoringDeviceId, $"Monitoring device with ID '{monitoringDeviceId}' was not found.");

public class MonitoringDeviceHasDependenciesException(
    Guid monitoringDeviceId)
    : MonitoringDeviceException(monitoringDeviceId,
        $"Monitoring device with ID '{monitoringDeviceId}' has dependencies and cannot be deleted.");

public class UnhandledMonitoringDeviceException(
    Guid monitoringDeviceId,
    Exception? innerException = null)
    : MonitoringDeviceException(monitoringDeviceId, "Unexpected error occurred.", innerException);