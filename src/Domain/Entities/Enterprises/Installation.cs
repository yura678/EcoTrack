using Domain.Common;
using Domain.Entities.EmissionSources;
using Domain.Entities.Monitoring;

namespace Domain.Entities.Enterprises;

public class Installation : BaseEntity
{
    public string Name { get; private set; }
    public Guid IedCategoryId { get; private set; }
    public IedCategory? IedCategory { get; private set; }
    public Guid SiteId { get; private set; }
    public Site? Site { get; private set; }
    public InstallationStatus Status { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<MonitoringPlan>? MonitoringPlans { get; private set; } = [];
    public ICollection<EmissionSource>? EmissionSources { get; private set; } = [];


    private Installation(Guid id, string name, Guid iedCategoryId, Guid siteId,
        InstallationStatus status,
        DateTime createdAt, DateTime? updatedAt)
    {
        Id = id;
        Name = name;
        IedCategoryId = iedCategoryId;
        SiteId = siteId;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }


    public static Installation New(Guid id, string name, Guid iedCategoryId, Guid siteId,
        InstallationStatus status) =>
        new(id, name, iedCategoryId, siteId, status, DateTime.UtcNow, null);

    public void UpdateDetails(string name, Guid iedCategoryId)
    {
        Name = name;
        IedCategoryId = iedCategoryId;

        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeStatus(InstallationStatus status)
    {
        Status = status;

        UpdatedAt = DateTime.UtcNow;
    }
}

public enum InstallationStatus
{
    // Працює у штатному режимі
    Operating = 0,

    // Тимчасово зупинена (напр для ремонту)
    TemporarilyShutDown = 1,

    // Повністю виведена з експлуатації
    Decommissioned = 2,

    // Ще будується або планується
    UnderConstruction = 3
}