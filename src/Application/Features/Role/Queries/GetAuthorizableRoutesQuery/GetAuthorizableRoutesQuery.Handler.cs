using Application.Common.Interfaces.Identity;
using MediatR;

namespace Application.Features.Role.Queries.GetAuthorizableRoutesQuery;

internal class GetAuthorizableRoutesQueryHandler(
    IRoleManagerService roleManagerService)
    : IRequestHandler<GetAuthorizableRoutesQuery, List<GetAuthorizableRoutesQueryResponse>>
{
    public async Task<List<GetAuthorizableRoutesQueryResponse>> Handle(
        GetAuthorizableRoutesQuery request,
        CancellationToken cancellationToken)
    {
        var authRoutes = await roleManagerService.GetPermissionActionsAsync();

        var result = authRoutes.Select(c =>
                new GetAuthorizableRoutesQueryResponse(
                    c.Key,
                    c.AreaName,
                    c.ControllerName,
                    c.ActionName,
                    c.ControllerDescription))
            .ToList();

        return result;
    }
}