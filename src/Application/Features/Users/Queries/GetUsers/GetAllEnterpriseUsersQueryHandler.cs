using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries;
using MediatR;

namespace Application.Features.Users.Queries.GetUsers;

internal class GetAllEnterpriseUsersQueryHandler(
    ICurrentUserService currentUserService,
    IUserEnterpriseMembershipQueries membershipQueries)
    : IRequestHandler<GetEnterpriseUsersQuery, List<GetUsersQueryResponse>>
{
    public async Task<List<GetUsersQueryResponse>> Handle(
        GetEnterpriseUsersQuery request,
        CancellationToken cancellationToken)
    {
        var enterpriseId = currentUserService.GetCurrentEnterpriseId();
        if (!enterpriseId.HasValue)
        {
            return [];
        }

        var rows = await membershipQueries.GetAdminListAsync(enterpriseId.Value, cancellationToken);
        return rows.ToList();
    }
}
