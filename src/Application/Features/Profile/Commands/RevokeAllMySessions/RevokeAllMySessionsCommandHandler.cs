using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Commands.RevokeAllMySessions;

/// <summary>
/// "Log out everywhere" — flips IsValid=false on every live refresh token of the caller.
/// The current device's session is killed too; the next API call after this will fail and
/// the user will have to re-authenticate.
/// </summary>
internal class RevokeAllMySessionsCommandHandler(
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RevokeAllMySessionsCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(
        RevokeAllMySessionsCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId is null) return new UserNotFoundException(Guid.Empty);

        await unitOfWork.UserRefreshTokenRepository
            .InvalidateAllForUserAsync(userId.Value, cancellationToken);
        return true;
    }
}
