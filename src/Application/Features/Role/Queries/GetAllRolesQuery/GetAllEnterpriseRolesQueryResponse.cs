namespace Application.Features.Role.Queries.GetAllRolesQuery;

public record GetAllEnterpriseRolesQueryResponse(
    Guid Id, string Name, Guid? EnterpriseId, string? EnterpriseName, bool IsSystem);
