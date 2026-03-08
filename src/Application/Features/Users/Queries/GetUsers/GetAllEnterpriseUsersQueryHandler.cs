using Application.Common.Interfaces.Identity;
using MediatR;

namespace Application.Features.Users.Queries.GetUsers;

internal class GetAllEnterpriseUsersQueryHandler(
    IAppUserManager userManager)
    : IRequestHandler<GetEnterpriseUsersQuery, List<GetUsersQueryResponse>>
{
    public async Task<List<GetUsersQueryResponse>> Handle(GetEnterpriseUsersQuery request,
        CancellationToken cancellationToken)
    {
        var usersModel =
            (await userManager.GetAllEnterpriseUsersAsync()).Select(u =>
                new GetUsersQueryResponse
                {
                    UserName = u.UserName,
                    UserId = u.Id,
                    Email = u.Email
                }).ToList();


        return usersModel;
    }
}