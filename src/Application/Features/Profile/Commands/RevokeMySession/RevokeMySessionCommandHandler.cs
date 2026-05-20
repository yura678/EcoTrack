using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Commands.RevokeMySession;

/// <summary>
/// Self-service revoke of a single refresh-token session. The repo method scopes the update
/// to (id, userId) so a leaked sessionId can never be revoked across users.
/// </summary>
internal class RevokeMySessionCommandHandler(
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RevokeMySessionCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(
        RevokeMySessionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId is null) return new UserNotFoundException(Guid.Empty);

        var revoked = await unitOfWork.UserRefreshTokenRepository
            .InvalidateForUserAsync(request.SessionId, userId.Value, cancellationToken);
        // Idempotent: revoking an already-invalid or non-existent (for this user) session is
        // not an error — the post-condition "this session is not live" already holds.
        return revoked;
    }
}
