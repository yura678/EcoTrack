namespace Application.Features.Role.Queries.GetAllRolesQuery;

public record GetAllRolesQueryResponse(
    Guid Id, string Name, Guid? EnterpriseId, string? EnterpriseName, bool IsSystem);
