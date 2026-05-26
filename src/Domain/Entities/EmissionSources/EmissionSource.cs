using Domain.Common;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using NetTopologySuite.Geometries;

namespace Domain.Entities.EmissionSources;

public class EmissionSource : BaseEntity, ISoftDeletable, ITenantOwned
{
    public string Code { get; protected set; }
    public Guid InstallationId { get; protected set; }
    public Installation? Installation { get; private set; }
    public Guid EnterpriseId { get; protected set; }
    public Point Location { get; protected set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; protected set; }
    public DateTime? DeletedAt { get; protected set; }

    public ICollection<Measurement>? Measurements { get; protected set; } = [];
    public ICollection<EmissionLimit>? EmissionLimits { get; protected set; } = [];
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

    public void UpdateCode(string code)
    {
        Code = code;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDeleted() => DeletedAt = DateTime.UtcNow;
    public void Restore() => DeletedAt = null;

    public void AssignTenant(Guid enterpriseId)
    {
        if (EnterpriseId == Guid.Empty)
        {
            EnterpriseId = enterpriseId;
        }
        else if (EnterpriseId != enterpriseId)
        {
            throw new InvalidOperationException(
                $"EnterpriseId is immutable on EmissionSource (current: {EnterpriseId}, attempted: {enterpriseId}).");
        }
    }
}