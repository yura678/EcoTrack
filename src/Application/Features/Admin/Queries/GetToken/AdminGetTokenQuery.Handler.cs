using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Features.Admin.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Admin.Queries.GetToken;

public class AdminGetTokenQueryHandler(
    IAppUserManager userManager,
    IJwtService jwtService)
    : IRequestHandler<AdminGetTokenQuery, Either<AdminException, AdminGetTokenQueryResult>>
{
    public async Task<Either<AdminException, AdminGetTokenQueryResult>> Handle(
        AdminGetTokenQuery request,
        CancellationToken cancellationToken)
    {
        return await CheckUserName(request.UserName)
            .BindAsync(user => GetTokenQuery(request, user, cancellationToken));
    }


    public async Task<Either<AdminException, AdminGetTokenQueryResult>> GetTokenQuery(
        AdminGetTokenQuery request,
        User user,
        CancellationToken cancellationToken)
    {
        var isUserLockedOut = await userManager.IsUserLockedOutAsync(user);

        if (isUserLockedOut)
            if (user.LockoutEnd != null)
                return new UserIsLockedException(user.Id, user.LockoutEnd.Value);

        var userRoles = await userManager.GetRoleAsync(user);

        if (userRoles.Length == 0)
            return new UserHasNoRolesException(user.Id);

        if (!await userManager.IsPasswordValidAsync(user, request.Password))
            return new InvalidCredentialsException(user.Id);

        var token = await jwtService.GenerateAsync(user, cancellationToken);

        return new AdminGetTokenQueryResult(token, userRoles);
    }

    private async Task<Either<AdminException, User>> CheckUserName(string userName)
    {
        var user = await userManager.GetByUserName(userName);

        return user.Match<Either<AdminException, User>>(
            u => u,
            () => new UserNotFoundException(Guid.Empty, userName)
        );
    }
}