using Application.Models.Auth;

namespace Application.Models.Profile;

/// <summary>
/// Self-service profile payload — what /api/v1/profile/me returns. Memberships reuse the same
/// shape as the auth /memberships endpoint so the frontend can share the rendering component.
/// </summary>
public record MyProfileInfo(
    Guid UserId,
    string Email,
    bool EmailConfirmed,
    string? Name,
    string? FamilyName,
    IReadOnlyList<MembershipInfo> Memberships);
