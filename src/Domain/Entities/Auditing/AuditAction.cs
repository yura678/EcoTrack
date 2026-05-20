namespace Domain.Entities.Auditing;

/// <summary>
/// Catalog of admin actions captured by the audit log. New entries append to the end so
/// existing rows keep their numeric meaning across deployments.
/// </summary>
public enum AuditAction
{
    UserInvited = 1,
    UserCreated = 2,
    UserRoleChanged = 3,
    UserMembershipRevoked = 4,
    UserPasswordReset = 5,
    UserAccountUnlocked = 6,
    RoleCreated = 7,
    RoleDeleted = 8,
    RolePermissionsChanged = 9
}
