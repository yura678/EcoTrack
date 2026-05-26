using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Enterprises;
using MediatR;

namespace Application.Features.Role.Queries.GetAllRolesQuery;

internal class GetAllRolesQueryHandler(
    IRoleManagerService roleManagerService,
    IEnterpriseQueries enterpriseQueries)
    : IRequestHandler<GetAllRolesQuery, List<GetAllRolesQueryResponse>>
{
    public async Task<List<GetAllRolesQueryResponse>> Handle(
        GetAllRolesQuery request,
        CancellationToken cancellationToken)
    {
        var roles = await roleManagerService.GetRolesAsync();
        var enterprises = await enterpriseQueries.GetAllAsync(cancellationToken);
        var nameById = enterprises.ToDictionary(e => e.Id, e => e.Name);

        var result = roles
            .Select(c => new GetAllRolesQueryResponse(
                Guid.Parse(c.Id),
                c.Name,
                c.EnterpriseId,
                c.EnterpriseId.HasValue && nameById.TryGetValue(c.EnterpriseId.Value, out var name)
                    ? name
                    : null,
                IsSystem: c.EnterpriseId == null))
            .ToList();

        return result;
    }
}
