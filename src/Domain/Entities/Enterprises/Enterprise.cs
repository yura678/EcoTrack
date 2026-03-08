using Domain.Common;
using Domain.Entities.User;

namespace Domain.Entities.Enterprises;

public class Enterprise : BaseEntity
{
    public string Name { get; private set; }
    public string Edrpou { get; }
    public RiskGroup RiskGroup { get; private set; }
    public string Address { get; private set; }
    public Guid SectorId { get; private set; }

    public Sector? Sector { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<Site>? Sites { get; private set; } = [];
    public ICollection<User.User>? Users { get; private set; } = [];
    public ICollection<EnterpriseInvitation>? Invitations { get; private set; } = [];

    private Enterprise(Guid id, string name, string edrpou, string address, RiskGroup riskGroup, Guid sectorId,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        Id = id;
        Name = name;
        Edrpou = edrpou;
        Address = address;
        RiskGroup = riskGroup;
        SectorId = sectorId;

        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Enterprise New(
        Guid id,
        string name,
        string edrpou,
        string address,
        RiskGroup riskGroup,
        Guid sectorId)
        => new(id, name, edrpou, address, riskGroup, sectorId, DateTime.UtcNow, null);

    public void UpdateDetails(string name, string address, RiskGroup riskGroup, Guid sectorId)
    {
        Name = name;
        Address = address;
        RiskGroup = riskGroup;
        SectorId = sectorId;

        UpdatedAt = DateTime.UtcNow;
    }
}

public enum RiskGroup
{
    None,
    Minor,
    Average,
    High,
}