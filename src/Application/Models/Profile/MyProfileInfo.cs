using Application.Models.Auth;

namespace Application.Models.Profile;

/// <summary>
/// Self-service profile payload — what /api/v1/profile/me returns. Memberships reuse the same
/// shape as the auth /memberships endpoint so the frontend can share the rendering component.
/// Roles + Permissions reflect the caller's effective access for the active enterprise (or
/// system-wide if the caller is superAdmin) — the SPA cannot decode the JWE-encrypted JWT
/// itself, so we project the claims here.
/// </summary>
public record MyProfileInfo(
    Guid UserId,
    string Email,
    bool EmailConfirmed,
    string? Name,
    string? FamilyName,
    Guid? ActiveEnterpriseId,
    bool IsSuperAdmin,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<MembershipInfo> Memberships);
