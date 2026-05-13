using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Auth.Exceptions;
using Application.Models.Jwt;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Commands.SwitchEnterprise;

public class SwitchEnterpriseCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IJwtService jwtService,
    IAppUserManager userManager)
    : IRequestHandler<SwitchEnterpriseCommand, Either<AuthException, AccessToken>>
{
    public async Task<Either<AuthException, AccessToken>> Handle(SwitchEnterpriseCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (!userId.HasValue)
            return new NotAuthenticatedException();

        var membership = await unitOfWork.UserEnterpriseMembershipRepository
            .GetByUserAndEnterpriseAsync(userId.Value, request.EnterpriseId, cancellationToken);

        return await membership.MatchAsync<UserEnterpriseMembership, Either<AuthException, AccessToken>>(
            Some: async m =>
            {
                if (!m.IsActive)
                    return new MembershipNotFoundException(userId.Value, request.EnterpriseId);

                var user = await userManager.GetUserByIdAsync(userId.Value);
                return await user.MatchAsync<User, Either<AuthException, AccessToken>>(
                    Some: async u =>
                    {
                        try
                        {
                            var token = await jwtService.GenerateAsync(u, request.EnterpriseId,
                                cancellationToken);
                            return token;
                        }
                        catch (Exception exception)
                        {
                            return new UnhandledAuthException(exception);
                        }
                    },
                    None: () => new NotAuthenticatedException());
            },
            None: () => new MembershipNotFoundException(userId.Value, request.EnterpriseId));
    }
}
