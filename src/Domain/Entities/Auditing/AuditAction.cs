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
    /// <summary>Password reset finalised (token consumed, hash rotated).</summary>
    UserPasswordResetCompleted = 5,
    UserAccountUnlocked = 6,
    RoleCreated = 7,
    RoleDeleted = 8,
    RolePermissionsChanged = 9,
    /// <summary>Admin pressed "force password reset" — reset link sent. Completion lands as <see cref="UserPasswordResetCompleted"/>.</summary>
    UserPasswordResetRequested = 10,
    /// <summary>Admin-initiated revoke of all the target user's sessions in the admin's enterprise.</summary>
    UserSessionsRevoked = 11,
    /// <summary>Admin replaced the user's email address. Details carry old/new addresses.</summary>
    UserEmailChanged = 12
}
