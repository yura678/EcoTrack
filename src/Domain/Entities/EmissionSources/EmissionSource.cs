using Domain.Common;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;

namespace Domain.Entities.EmissionSources;

public class EmissionSource : BaseEntity
{
    public string Code { get; protected set; }
    public Guid InstallationId { get; protected set; }
    public Installation? Installation { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; protected set; }

    public ICollection<Measurement>? Measurements { get; protected set; } = [];
    public ICollection<EmissionLimit>? EmissionLimits { get; protected set; } = [];
    public ICollection<MonitoringRequirement>? MonitoringRequirements { get; protected set; } = [];
    public ICollection<MonitoringDevice>? MonitoringDevices { get; protected set; } = [];

    protected EmissionSource(Guid id, Guid installationId, string code, DateTime createdAt,
        DateTime? updatedAt)
    {
        Id = id;
        InstallationId = installationId;
        Code = code;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }
}