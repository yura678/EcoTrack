using Domain.Common;
using NetTopologySuite.Geometries;

namespace Domain.Entities.Enterprises;

public class Site : BaseEntity
{
    public string Name { get; private set; }
    public string Address { get; private set; }
    public int? SanitaryZoneRadius { get; private set; }
    public Point? Location { get; private set; }
    public double? Elevation { get; private set; }
    public Guid EnterpriseId { get; private set; }
    public Enterprise? Enterprise { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<Installation>? Installations { get; private set; } = [];

    private Site(Guid id, string name, string address, int? sanitaryZoneRadius,
        Point? location, double? elevation,
        Guid enterpriseId, DateTime createdAt, DateTime? updatedAt)
    {
        Id = id;
        Name = name;
        Address = address;
        SanitaryZoneRadius = sanitaryZoneRadius;
        Location = location;
        Elevation = elevation;
        EnterpriseId = enterpriseId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Site New(
        Guid id,
        string name,
        string address,
        int? sanitaryZoneRadius,
        Guid enterpriseId,
        double? latitude = null,
        double? longitude = null,
        double? elevation = null)
    {
        var location = latitude.HasValue && longitude.HasValue
            ? new Point(longitude.Value, latitude.Value) { SRID = 4326 }
            : null;
        return new Site(id, name, address, sanitaryZoneRadius, location, elevation, enterpriseId,
            DateTime.UtcNow, null);
    }

    public void UpdateDetails(string name, string address, int? sanitaryZoneRadius,
        double? latitude, double? longitude, double? elevation)
    {
        Name = name;
        Address = address;
        SanitaryZoneRadius = sanitaryZoneRadius;
        Location = latitude.HasValue && longitude.HasValue
            ? new Point(longitude.Value, latitude.Value) { SRID = 4326 }
            : null;
        Elevation = elevation;

        UpdatedAt = DateTime.UtcNow;
    }
}