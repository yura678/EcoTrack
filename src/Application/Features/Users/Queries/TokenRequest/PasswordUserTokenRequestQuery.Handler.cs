using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Features.Users.Exceptions;
using Application.Models.Jwt;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Users.Queries.TokenRequest;

public class PasswordUserTokenRequestQueryResult(
    IAppUserManager userManager,
    IJwtService jwtService,
    ILogger<PasswordUserTokenRequestQueryResult> logger)
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
                    logger.LogWarning("Login attempt on locked user {UserId}", u.Id);
                    return new InvalidCredentialsException(Guid.Empty);
                }

                if (!await userManager.IsPasswordValidAsync(u, request.Password))
                {
                    await userManager.IncrementAccessFailedCountAsync(u);
                    logger.LogWarning("Failed login attempt for user {UserId}", u.Id);
                    return new InvalidCredentialsException(Guid.Empty);
                }

                await userManager.ResetUserLockoutAsync(u);

                var token = await jwtService.GenerateAsync(u, null, cancellationToken);

                return token;
            },
            None: () =>
            {
                logger.LogWarning("Login attempt with unknown username");
                return new InvalidCredentialsException(Guid.Empty);
            }
        );
    }
}
