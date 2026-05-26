using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Enterprises;
using MediatR;

namespace Application.Features.Role.Queries.GetAllRolesQuery;

public class GetAllEnterpriseRolesQueryHandler(
    IRoleManagerService roleManagerService,
    ICurrentUserService currentUserService,
    IEnterpriseQueries enterpriseQueries)
    : IRequestHandler<GetAllEnterpriseRolesQuery, List<GetAllEnterpriseRolesQueryResponse>>
{
    public async Task<List<GetAllEnterpriseRolesQueryResponse>> Handle(
        GetAllEnterpriseRolesQuery request,
        CancellationToken cancellationToken)
    {
        var enterpriseId = currentUserService.GetCurrentEnterpriseId();
        if (!enterpriseId.HasValue)
        {
            return [];
        }

        var roles = await roleManagerService.GetEnterpriseRolesAsync(enterpriseId.Value);
        var enterpriseOpt = await enterpriseQueries.GetByIdAsync(enterpriseId.Value, cancellationToken);
        var enterpriseName = enterpriseOpt.Match<string?>(e => e.Name, () => null);

        var result = roles
            .Select(c => new GetAllEnterpriseRolesQueryResponse(
                Guid.Parse(c.Id),
                c.Name,
                c.EnterpriseId,
                c.EnterpriseId.HasValue ? enterpriseName : null,
                IsSystem: c.EnterpriseId == null))
            .ToList();

        return result;
    }
}
