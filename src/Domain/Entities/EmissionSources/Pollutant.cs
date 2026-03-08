using Domain.Common;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;

namespace Domain.Entities.EmissionSources;

public class Pollutant : BaseEntity
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<EmissionLimit>? EmissionLimits { get; private set; } = [];
    public ICollection<Measurement>? Measurements { get; private set; } = [];
    public ICollection<MonitoringRequirement>? MonitoringRequirements { get; private set; } = [];

    private Pollutant(Guid id, string code, string name, DateTime createdAt, DateTime? updatedAt)
    {
        Id = id;
        Code = code;
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Pollutant New(Guid id, string code, string name) =>
        new(id, code, name, DateTime.UtcNow, null);

    public void UpdateDetails(string code, string name)
    {
        Code = code;
        Name = name;

        UpdatedAt = DateTime.UtcNow;
    }
}