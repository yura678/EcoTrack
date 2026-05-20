using Application.Models.Auth;

namespace Application.Models.Users;

/// <summary>
/// Admin user-profile payload — what /api/v1/users/{userId} returns. Membership is the row in
/// the admin's current tenant only; null when the caller is superAdmin viewing a user that
/// has no presence in their (potentially absent) enterprise context.
/// </summary>
public record UserDetailInfo(
    Guid UserId,
    string Email,
    bool EmailConfirmed,
    string? Name,
    string? FamilyName,
    DateTimeOffset? LockoutEnd,
    bool IsLockedOut,
    int AccessFailedCount,
    MembershipInfo? Membership);
