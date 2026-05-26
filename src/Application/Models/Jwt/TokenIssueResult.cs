namespace Application.Models.Jwt;

/// <summary>
/// What <see cref="Application.Common.Interfaces.IJwtService.GenerateAsync"/> returns to
/// callers — the issued token plus the enterprise context that was baked into it. Login
/// flows pass <c>null</c> for the preferred enterpriseId, so the resolved value reflects
/// the service's "pick first active membership" decision and is needed by the profile
/// builder to project the correct roles/permissions.
/// </summary>
public record TokenIssueResult(AccessToken Token, Guid? ResolvedEnterpriseId);
