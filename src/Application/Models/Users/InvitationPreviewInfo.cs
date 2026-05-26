namespace Application.Models.Users;

/// <summary>
/// Public-facing preview of an invitation, fetched by the recipient on the /join landing page
/// before they accept. The invitation token is itself the bearer secret, so this endpoint
/// is intentionally anonymous (same pattern as /reset-password tokens).
/// </summary>
public record InvitationPreviewInfo(
    string Email,
    string EnterpriseName,
    string RoleName,
    string? RoleDisplayName,
    DateTime ExpiresAt);
