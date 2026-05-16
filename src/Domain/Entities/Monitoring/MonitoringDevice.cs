using Domain.Common;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;

namespace Domain.Entities.Monitoring;

public class MonitoringDevice : BaseEntity, ITenantOwned
{
    public Guid? EmissionSourceId { get; private set; }
    public EmissionSource? EmissionSource { get; private set; }

    public Guid InstallationId { get; private set; }
    public Installation? Installation { get; private set; }
    public Guid EnterpriseId { get; private set; }
    public string Model { get; private set; }
    public string SerialNumber { get; private set; }
    public MonitoringDeviceType Type { get; private set; }
    public DateTime? InstalledAt { get; private set; }
    public DeviceStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public string? IngestionSecret { get; private set; }

    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<DevicePollutantCapability>? Capabilities { get; private set; } = [];
    public ICollection<CalibrationRecord>? Calibrations { get; private set; } = [];


    private MonitoringDevice(Guid id, Guid installationId,
        Guid? emissionSourceId, string model,
        string serialNumber, MonitoringDeviceType type, DateTime? installedAt, DateTime createdAt, DateTime? updatedAt,
        DeviceStatus status, string? notes)
    {
        Id = id;
        EmissionSourceId = emissionSourceId;
        InstallationId = installationId;
        Model = model;
        SerialNumber = serialNumber;
        Type = type;

        InstalledAt = installedAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Status = status;
        Notes = notes;
    }

    public static MonitoringDevice New(Guid id, Guid installationId,
        Guid? emissionSourceId,
        string model, string serialNumber, MonitoringDeviceType type, DeviceStatus status, string? notes)
    {
        DateTime? installedAt = emissionSourceId is null ? null : DateTime.UtcNow;

        return new MonitoringDevice(id, installationId, emissionSourceId, model, serialNumber, type, installedAt,
            DateTime.UtcNow, null, status,
            notes);
    }

    public void UpdateDetails(Guid? emissionSourceId, DeviceStatus status, string? notes)
    {
        EmissionSourceId = emissionSourceId;
        InstalledAt = emissionSourceId is null ? null : DateTime.UtcNow;
        Status = status;
        Notes = notes;

        UpdatedAt = DateTime.UtcNow;
    }

    public void RotateIngestionSecret(string newSecret)
    {
        IngestionSecret = newSecret;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Decommission()
    {
        if (Status == DeviceStatus.Decommissioned) return;
        Status = DeviceStatus.Decommissioned;
        IngestionSecret = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignTenant(Guid enterpriseId)
    {
        if (EnterpriseId == Guid.Empty)
        {
            EnterpriseId = enterpriseId;
        }
        else if (EnterpriseId != enterpriseId)
        {
            throw new InvalidOperationException(
                $"EnterpriseId is immutable on MonitoringDevice (current: {EnterpriseId}, attempted: {enterpriseId}).");
        }
    }
}

public enum MonitoringDeviceType
{
    CEMS = 0,
    Sampler = 1,
    FlowMeter = 2
}

public enum DeviceStatus
{
    Operational = 0,
    Offline = 1,
    Maintenance = 2,
    Decommissioned = 3
}