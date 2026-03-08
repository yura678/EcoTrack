using Application.Common.Interfaces.Identity;
using MediatR;

namespace Application.Features.Role.Queries.GetAllRolesQuery;

internal class GetAllRolesQueryHandler(IRoleManagerService roleManagerService)
    : IRequestHandler<GetAllRolesQuery, List<GetAllRolesQueryResponse>>
{
    public async Task<List<GetAllRolesQueryResponse>> Handle(
        GetAllRolesQuery request,
        CancellationToken cancellationToken)
    {
        var roles = await roleManagerService.GetRolesAsync();

        var result = roles.Select(c => new GetAllRolesQueryResponse(Guid.Parse(c.Id), c.Name, c.EnterpirseId)).ToList();

        return result;
    }
}