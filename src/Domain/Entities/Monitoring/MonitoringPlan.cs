using Domain.Common;
using Domain.Entities.Enterprises;

namespace Domain.Entities.Monitoring;

public class MonitoringPlan : BaseEntity
{
    public Guid InstallationId { get; private set; }
    public Installation? Installation { get; private set; }
    public string Version { get; private set; }
    public MonitoringPlanStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }


    public ICollection<MonitoringRequirement>? Requirements { get; private set; } = [];


    private MonitoringPlan(Guid id, Guid installationId, string version,
        MonitoringPlanStatus status, DateTime createdAt, DateTime? updatedAt, string? notes)
    {
        Id = id;
        InstallationId = installationId;
        Version = version;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Notes = notes;
    }

    public static MonitoringPlan New(Guid id, Guid installationId, string version,
        string? notes, ICollection<MonitoringRequirement> requirements) =>
        new(id, installationId, version, MonitoringPlanStatus.Draft, DateTime.UtcNow, null, notes)
        {
            Requirements = requirements
        };

    public void UpdateDetails(string? notes, string version)
    {
        Notes = notes;
        Version = version;

        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeStatus(MonitoringPlanStatus status)
    {
        Status = status;

        UpdatedAt = DateTime.UtcNow;
    }
}

public enum MonitoringPlanStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2,
    Revoked = 3
}