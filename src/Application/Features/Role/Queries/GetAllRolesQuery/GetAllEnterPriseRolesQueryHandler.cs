using Application.Common.Interfaces.Identity;
using MediatR;

namespace Application.Features.Role.Queries.GetAllRolesQuery;

public class GetAllEnterpriseRolesQueryHandler(
    IRoleManagerService roleManagerService,
    ICurrentUserService currentUserService)
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

        var result = roles.Select(c => new GetAllEnterpriseRolesQueryResponse(Guid.Parse(c.Id), c.Name)).ToList();

        return result;
    }
}