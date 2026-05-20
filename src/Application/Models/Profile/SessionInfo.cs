namespace Application.Models.Profile;

/// <summary>
/// One active refresh-token entry for the self-service sessions view. EnterpriseId is null
/// for legacy tokens minted before multi-tenancy or for sessions issued without an active
/// enterprise context; EnterpriseName mirrors that nullability.
/// </summary>
public record SessionInfo(
    Guid Id,
    Guid? EnterpriseId,
    string? EnterpriseName,
    DateTime CreatedAt,
    DateTime ExpiresAt);
