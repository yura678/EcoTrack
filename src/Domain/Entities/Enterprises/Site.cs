using Domain.Common;

namespace Domain.Entities.Enterprises;

public class Site : BaseEntity
{
    public string Name { get; private set; }
    public string Address { get; private set; }
    public int? SanitaryZoneRadius { get; private set; }
    public Guid EnterpriseId { get; private set; }
    public Enterprise? Enterprise { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<Installation>? Installations { get; private set; } = [];

    private Site(Guid id, string name, string address, int? sanitaryZoneRadius,
        Guid enterpriseId, DateTime createdAt, DateTime? updatedAt)
    {
        Id = id;
        Name = name;
        Address = address;
        SanitaryZoneRadius = sanitaryZoneRadius;
        EnterpriseId = enterpriseId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Site New(
        Guid id,
        string name,
        string address,
        int? sanitaryZoneRadius,
        Guid enterpriseId) => new(id, name, address, sanitaryZoneRadius, enterpriseId,
        DateTime.UtcNow, null);

    public void UpdateDetail(string name, string address, int? sanitaryZoneRadius)
    {
        Name = name;
        Address = address;
        SanitaryZoneRadius = sanitaryZoneRadius;

        UpdatedAt = DateTime.UtcNow;
    }
}