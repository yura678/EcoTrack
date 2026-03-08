using Application.Common.Interfaces.Identity;
using MediatR;

namespace Application.Features.Users.Queries.GetUsers;

internal class GetUsersQueryHandler(
    IAppUserManager userManager)
    : IRequestHandler<GetUsersQuery, List<GetUsersQueryResponse>>
{
    public async Task<List<GetUsersQueryResponse>> Handle(GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        var usersModel =
            (await userManager.GetAllUsersAsync()).Select(u =>
                new GetUsersQueryResponse
                {
                    UserName = u.UserName,
                    UserId = u.Id,
                    Email = u.Email
                }).ToList();


        return usersModel;
    }
}