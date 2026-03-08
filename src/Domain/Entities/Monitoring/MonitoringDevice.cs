using Domain.Common;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;

namespace Domain.Entities.Monitoring;

public class MonitoringDevice : BaseEntity
{
    public Guid? EmissionSourceId { get; private set; }
    public EmissionSource? EmissionSource { get; private set; }

    public Guid InstallationId { get; private set; }
    public Installation? Installation { get; private set; }
    public string Model { get; private set; }
    public string SerialNumber { get; private set; }
    public MonitoringDeviceType Type { get; private set; }
    public DateTime? InstalledAt { get; private set; }
    public bool IsOnline { get; private set; }
    public string? Notes { get; private set; }

    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }


    private MonitoringDevice(Guid id, Guid installationId,
        Guid? emissionSourceId, string model,
        string serialNumber, MonitoringDeviceType type, DateTime? installedAt, DateTime createdAt, DateTime? updatedAt,
        bool isOnline, string? notes)
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
        IsOnline = isOnline;
        Notes = notes;
    }

    public static MonitoringDevice New(Guid id, Guid installationId,
        Guid? emissionSourceId,
        string model, string serialNumber, MonitoringDeviceType type, bool isOnline, string? notes)
    {
        DateTime? installedAt = emissionSourceId is null ? null : DateTime.UtcNow;

        return new MonitoringDevice(id, installationId, emissionSourceId, model, serialNumber, type, installedAt,
            DateTime.UtcNow, null, isOnline,
            notes);
    }

    public void UpdateDetails(Guid? emissionSourceId, bool isOnline, string? notes)
    {
        EmissionSourceId = emissionSourceId;
        InstalledAt = emissionSourceId is null ? null : DateTime.UtcNow;
        IsOnline = isOnline;
        Notes = notes;

        UpdatedAt = DateTime.UtcNow;
    }
}

public enum MonitoringDeviceType
{
    CEMS = 0,
    Sampler = 1,
    FlowMeter = 2
}