using Domain.Common;
using Domain.Entities.Enterprises;

namespace Domain.Entities.User;

public class UserEnterpriseMembership : BaseEntity
{
    public Guid UserId { get; private set; }
    public User? User { get; private set; }
    public Guid EnterpriseId { get; private set; }
    public Enterprise? Enterprise { get; private set; }
    public Guid RoleId { get; private set; }
    public Role? Role { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    private UserEnterpriseMembership(Guid id, Guid userId, Guid enterpriseId, Guid roleId,
        DateTime joinedAt, DateTime? revokedAt)
    {
        Id = id;
        UserId = userId;
        EnterpriseId = enterpriseId;
        RoleId = roleId;
        JoinedAt = joinedAt;
        RevokedAt = revokedAt;
    }

    public static UserEnterpriseMembership New(Guid id, Guid userId, Guid enterpriseId, Guid roleId) =>
        new(id, userId, enterpriseId, roleId, DateTime.UtcNow, null);

    public void Revoke()
    {
        RevokedAt = DateTime.UtcNow;
    }

    public void Reinstate(Guid roleId)
    {
        RoleId = roleId;
        RevokedAt = null;
    }

    public void ChangeRole(Guid roleId)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot change role of a revoked membership.");
        RoleId = roleId;
    }
}
