using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Auth.Exceptions;
using Application.Models.Auth;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Auth.Commands.SwitchEnterprise;

public class SwitchEnterpriseCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IJwtService jwtService,
    IUserProfileBuilder profileBuilder,
    IAppUserManager userManager)
    : IRequestHandler<SwitchEnterpriseCommand, Either<AuthException, AuthSession>>
{
    public async Task<Either<AuthException, AuthSession>> Handle(SwitchEnterpriseCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (!userId.HasValue)
            return new NotAuthenticatedException();

        var membership = await unitOfWork.UserEnterpriseMembershipRepository
            .GetByUserAndEnterpriseAsync(userId.Value, request.EnterpriseId, cancellationToken);

        return await membership.MatchAsync<UserEnterpriseMembership, Either<AuthException, AuthSession>>(
            Some: async m =>
            {
                if (!m.IsActive)
                    return new MembershipNotFoundException(userId.Value, request.EnterpriseId);

                var user = await userManager.GetUserByIdAsync(userId.Value);
                return await user.MatchAsync<User, Either<AuthException, AuthSession>>(
                    Some: async u =>
                    {
                        try
                        {
                            var issued = await jwtService.GenerateAsync(u, request.EnterpriseId,
                                cancellationToken);
                            var profile = await profileBuilder.BuildAsync(
                                u, issued.ResolvedEnterpriseId, cancellationToken);
                            return new AuthSession(issued.Token, profile);
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
