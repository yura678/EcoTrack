using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Features.Admin.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Admin.Queries.GetToken;

public class AdminGetTokenQueryHandler(
    IAppUserManager userManager,
    IJwtService jwtService,
    ILogger<AdminGetTokenQueryHandler> logger)
    : IRequestHandler<AdminGetTokenQuery, Either<AdminException, AdminGetTokenQueryResult>>
{
    public async Task<Either<AdminException, AdminGetTokenQueryResult>> Handle(
        AdminGetTokenQuery request,
        CancellationToken cancellationToken)
    {
        var userOption = await userManager.GetByUserName(request.UserName);

        return await userOption.MatchAsync<User, Either<AdminException, AdminGetTokenQueryResult>>(
            Some: async user =>
            {
                if (await userManager.IsUserLockedOutAsync(user))
                {
                    logger.LogWarning("Admin login attempt on locked user {UserId}", user.Id);
                    return new InvalidCredentialsException(Guid.Empty);
                }

                if (!await userManager.IsPasswordValidAsync(user, request.Password))
                {
                    await userManager.IncrementAccessFailedCountAsync(user);
                    logger.LogWarning("Failed admin login for user {UserId}", user.Id);
                    return new InvalidCredentialsException(Guid.Empty);
                }

                var userRoles = await userManager.GetRoleAsync(user);
                if (userRoles.Length == 0)
                {
                    logger.LogWarning("Admin login by user {UserId} without roles", user.Id);
                    return new InvalidCredentialsException(Guid.Empty);
                }

                await userManager.ResetUserLockoutAsync(user);

                var token = await jwtService.GenerateAsync(user, null, cancellationToken);
                return new AdminGetTokenQueryResult(token, userRoles);
            },
            None: () =>
            {
                logger.LogWarning("Admin login attempt with unknown username");
                return new InvalidCredentialsException(Guid.Empty);
            }
        );
    }
}
