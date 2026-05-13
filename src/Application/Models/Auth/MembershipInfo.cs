namespace Application.Models.Auth;

public record MembershipInfo(
    Guid EnterpriseId,
    string EnterpriseName,
    Guid RoleId,
    string RoleName,
    string? RoleDisplayName,
    DateTime JoinedAt);
