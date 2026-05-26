using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Commands.RevokeAllMySessions;

/// <summary>
/// "Log out everywhere" — both invalidates every live refresh token AND rotates the user's
/// security stamp. The stamp bump is what makes access tokens (JWE) issued before this call
/// fail on the next request via <c>OnTokenValidated → ValidateSecurityStampAsync</c>; without
/// it the access tokens would remain valid until their natural TTL (60 min) elapses, which
/// defeats the "kick the attacker out NOW" intent of this command.
/// </summary>
internal class RevokeAllMySessionsCommandHandler(
    ICurrentUserService currentUserService,
    IAppUserManager userManager,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RevokeAllMySessionsCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(
        RevokeAllMySessionsCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId is null) return new UserNotFoundException(Guid.Empty);

        var userOption = await userManager.GetUserByIdAsync(userId.Value);
        return await userOption.MatchAsync<User, Either<UserException, bool>>(
            Some: async user =>
            {
                await unitOfWork.UserRefreshTokenRepository
                    .InvalidateAllForUserAsync(user.Id, cancellationToken);
                await userManager.UpdateSecurityStampAsync(user);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            },
            None: () => new UserNotFoundException(userId.Value));
    }
}
