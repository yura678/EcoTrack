using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Features.Users.Exceptions;
using Application.Models.Jwt;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Queries.TokenRequest;

public class PasswordUserTokenRequestQueryResult(
    IAppUserManager userManager,
    IJwtService jwtService)
    : IRequestHandler<PasswordUserTokenRequestQuery, Either<UserException, AccessToken>>
{
    public async Task<Either<UserException, AccessToken>> Handle(
        PasswordUserTokenRequestQuery request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetByUserName(request.UserName);

        return await user.MatchAsync<User, Either<UserException, AccessToken>>(
            Some: async u =>
            {
                
                if (await userManager.IsUserLockedOutAsync(u))
                {
                    return new UserIsLockedException(u.Id, u.LockoutEnd!.Value);
                }

                if (!await userManager.IsPasswordValidAsync(u, request.Password))
                {
                    return new InvalidCredentialsException(u.Id);
                }

                var token = await jwtService.GenerateAsync(u, cancellationToken);

                return token;
            },
            None: () => new InvalidCredentialsException(Guid.Empty)
        );
    }
}