using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Features.Users.Exceptions;
using Application.Models.Jwt;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Auth.Queries.LoginByPassword;

internal class LoginByPasswordQueryHandler(
    IAppUserManager userManager,
    IJwtService jwtService,
    ILogger<LoginByPasswordQueryHandler> logger)
    : IRequestHandler<LoginByPasswordQuery, Either<UserException, AccessToken>>
{
    public async Task<Either<UserException, AccessToken>> Handle(LoginByPasswordQuery request,
        CancellationToken cancellationToken)
    {
        var userOption = await userManager.GetUserByEmail(request.Email);

        return await userOption.MatchAsync<User, Either<UserException, AccessToken>>(
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

                if (!u.EmailConfirmed)
                    return new UserVerificationException(u.Id, "Email is not confirmed.");

                await userManager.ResetUserLockoutAsync(u);
                return await jwtService.GenerateAsync(u, null, cancellationToken);
            },
            None: () =>
            {
                logger.LogWarning("Login attempt with unknown email");
                return new InvalidCredentialsException(Guid.Empty);
            });
    }
}
