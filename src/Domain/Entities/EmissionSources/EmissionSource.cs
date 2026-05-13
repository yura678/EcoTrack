using Domain.Common;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using NetTopologySuite.Geometries;

namespace Domain.Entities.EmissionSources;

public class EmissionSource : BaseEntity
{
    public string Code { get; protected set; }
    public Guid InstallationId { get; protected set; }
    public Installation? Installation { get; private set; }
    public Point Location { get; protected set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; protected set; }

    public ICollection<Measurement>? Measurements { get; protected set; } = [];
    public ICollection<EmissionLimit>? EmissionLimits { get; protected set; } = [];
    public ICollection<MonitoringRequirement>? MonitoringRequirements { get; protected set; } = [];
    public ICollection<MonitoringDevice>? MonitoringDevices { get; protected set; } = [];

    protected EmissionSource(Guid id, Guid installationId, string code, Point location,
        DateTime createdAt, DateTime? updatedAt)
    {
        Id = id;
        InstallationId = installationId;
        Code = code;
        Location = location;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    protected static Point BuildPoint(double latitude, double longitude) =>
        new(longitude, latitude) { SRID = 4326 };

    public void UpdateLocation(double latitude, double longitude)
    {
        Location = BuildPoint(latitude, longitude);
        UpdatedAt = DateTime.UtcNow;
    }
}