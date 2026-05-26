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
        $"A monitoring device with serial number '{serialNumber}' already exists.")
{
    public string SerialNumber { get; } = serialNumber;
}

public class EmissionSourceNotFoundException(
    Guid monitoringDeviceId,
    Guid emissionSourceId)
    : MonitoringDeviceException(monitoringDeviceId, "Emission source not found.")
{
    public Guid EmissionSourceId { get; } = emissionSourceId;
}

public class InstallationNotFoundException(
    Guid monitoringDeviceId,
    Guid installationId)
    : MonitoringDeviceException(monitoringDeviceId, "Installation not found.")
{
    public Guid InstallationId { get; } = installationId;
}

public class MonitoringDeviceNotFoundException(
    Guid monitoringDeviceId)
    : MonitoringDeviceException(monitoringDeviceId, "Monitoring device not found.");

public class MonitoringDeviceHasDependenciesException(
    Guid monitoringDeviceId)
    : MonitoringDeviceException(monitoringDeviceId,
        "Monitoring device has related data (measurements, calibrations) and cannot be deleted.");

public class InvalidEmissionSourceInstallationException(
    Guid monitoringDeviceId,
    Guid sourceId,
    Guid expectedInstallationId,
    Guid actualInstallationId)
    : MonitoringDeviceException(monitoringDeviceId,
        "Emission source belongs to a different installation than the device. " +
        "Pick a source from the same installation.")
{
    public Guid SourceId { get; } = sourceId;
    public Guid ExpectedInstallationId { get; } = expectedInstallationId;
    public Guid ActualInstallationId { get; } = actualInstallationId;
}

/// <summary>
/// Operator tried to set the device to anything other than Decommissioned while its parent
/// installation is itself Decommissioned. Bringing the device back to life implies the
/// installation is alive — so we force the operator to reactivate the installation first.
/// </summary>
public class ParentInstallationDecommissionedException(
    Guid monitoringDeviceId,
    Guid installationId)
    : MonitoringDeviceException(monitoringDeviceId,
        "Device cannot be activated while its installation is shut down. " +
        "Reactivate the installation first.")
{
    public Guid InstallationId { get; } = installationId;
}

public class UnhandledMonitoringDeviceException(
    Guid monitoringDeviceId,
    Exception? innerException = null)
    : MonitoringDeviceException(monitoringDeviceId, "Unexpected error occurred.", innerException);
