namespace Application.Features.Users.Queries.GetUsers;

public record GetUsersQueryResponse(
    Guid Id,
    string Email,
    string UserName,
    string? Name,
    string? FamilyName,
    Guid? RoleId,
    string? Role,
    bool IsActive,
    bool IsLocked,
    bool EmailConfirmed,
    DateTime? LastLoginAt,
    Guid? EnterpriseId,
    string? EnterpriseName);
