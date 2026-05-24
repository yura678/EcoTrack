using Domain.Common;
using Domain.Entities.User;

namespace Domain.Entities.Enterprises;

public class Enterprise : BaseEntity, ISoftDeletable
{
    public string Name { get; private set; }
    public string Edrpou { get; }
    public RiskGroup RiskGroup { get; private set; }
    public string Address { get; private set; }
    public Guid SectorId { get; private set; }

    public Sector? Sector { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// Lifecycle state from the SuperAdmin's perspective. New self-service registrations
    /// start as <see cref="EnterpriseStatus.Pending"/> so a real person has to vet the
    /// EDRPOU claim before any user can log into the tenant. Existing rows from before
    /// approval was introduced are migrated as <see cref="EnterpriseStatus.Active"/>.
    /// </summary>
    public EnterpriseStatus Status { get; private set; }
    public DateTime? ApprovalDecisionAt { get; private set; }
    public Guid? ApprovalDecisionByUserId { get; private set; }
    public string? RejectionReason { get; private set; }

    public ICollection<Site>? Sites { get; private set; } = [];
    public ICollection<User.UserEnterpriseMembership>? Memberships { get; private set; } = [];
    public ICollection<EnterpriseInvitation>? Invitations { get; private set; } = [];

    private Enterprise(Guid id, string name, string edrpou, string address, RiskGroup riskGroup, Guid sectorId,
        EnterpriseStatus status,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        Id = id;
        Name = name;
        Edrpou = edrpou;
        Address = address;
        RiskGroup = riskGroup;
        SectorId = sectorId;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Creates an enterprise awaiting SuperAdmin verification. Use this for any self-service
    /// flow — the registrant cannot be trusted to claim an EDRPOU code until a human checks
    /// it against the official registry.
    /// </summary>
    public static Enterprise NewPending(
        Guid id,
        string name,
        string edrpou,
        string address,
        RiskGroup riskGroup,
        Guid sectorId)
        => new(id, name, edrpou, address, riskGroup, sectorId,
            EnterpriseStatus.Pending, DateTime.UtcNow, null);

    /// <summary>
    /// Creates an already-active enterprise. Reserved for SuperAdmin-driven flows (seed,
    /// imports, tests) where verification has been performed out of band.
    /// </summary>
    public static Enterprise NewActive(
        Guid id,
        string name,
        string edrpou,
        string address,
        RiskGroup riskGroup,
        Guid sectorId)
        => new(id, name, edrpou, address, riskGroup, sectorId,
            EnterpriseStatus.Active, DateTime.UtcNow, null);

    public void UpdateDetails(string name, string address, RiskGroup riskGroup, Guid sectorId)
    {
        Name = name;
        Address = address;
        RiskGroup = riskGroup;
        SectorId = sectorId;

        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve(Guid decisionByUserId)
    {
        if (Status == EnterpriseStatus.Active) return;
        if (Status == EnterpriseStatus.Rejected)
            throw new InvalidOperationException(
                "Rejected enterprise cannot be approved; re-register or restore manually.");
        Status = EnterpriseStatus.Active;
        ApprovalDecisionAt = DateTime.UtcNow;
        ApprovalDecisionByUserId = decisionByUserId;
        RejectionReason = null;
    }

    public void Reject(Guid decisionByUserId, string reason)
    {
        if (Status == EnterpriseStatus.Active)
            throw new InvalidOperationException(
                "Active enterprise cannot be rejected; use Suspend instead.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required.", nameof(reason));
        Status = EnterpriseStatus.Rejected;
        ApprovalDecisionAt = DateTime.UtcNow;
        ApprovalDecisionByUserId = decisionByUserId;
        RejectionReason = reason.Trim();
    }

    public void MarkDeleted() => DeletedAt = DateTime.UtcNow;
    public void Restore() => DeletedAt = null;
}

public enum RiskGroup
{
    None,
    Minor,
    Average,
    High,
}

public enum EnterpriseStatus
{
    Pending = 0,
    Active = 1,
    Rejected = 2,
    Suspended = 3
}
