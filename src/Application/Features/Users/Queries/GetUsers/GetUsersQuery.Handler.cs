using Application.Common.Interfaces.Queries;
using MediatR;

namespace Application.Features.Users.Queries.GetUsers;

internal class GetUsersQueryHandler(IUserEnterpriseMembershipQueries membershipQueries)
    : IRequestHandler<GetUsersQuery, List<GetUsersQueryResponse>>
{
    public async Task<List<GetUsersQueryResponse>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        // superAdmin path — cross-tenant listing, no enterprise filter.
        var rows = await membershipQueries.GetAdminListAsync(null, cancellationToken);
        return rows.ToList();
    }
}
