namespace Application.Features.Role.Queries.GetAllRolesQuery;

public record GetAllRolesQueryResponse(Guid RoleId, string RoleName, Guid? EnterpriseId);