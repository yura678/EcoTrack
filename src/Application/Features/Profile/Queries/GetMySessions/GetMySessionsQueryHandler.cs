using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Application.Models.Profile;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Queries.GetMySessions;

internal class GetMySessionsQueryHandler(
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<GetMySessionsQuery, Either<UserException, IReadOnlyList<SessionInfo>>>
{
    public async Task<Either<UserException, IReadOnlyList<SessionInfo>>> Handle(
        GetMySessionsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId is null) return new UserNotFoundException(Guid.Empty);

        var sessions = await unitOfWork.UserRefreshTokenRepository
            .GetActiveSessionsForUserAsync(userId.Value, cancellationToken);
        return Either<UserException, IReadOnlyList<SessionInfo>>.Right(sessions);
    }
}
