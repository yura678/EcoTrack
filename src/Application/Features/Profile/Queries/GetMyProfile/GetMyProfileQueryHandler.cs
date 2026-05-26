using Application.Common.Interfaces.Identity;
using Application.Features.Users.Exceptions;
using Application.Models.Profile;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Queries.GetMyProfile;

/// <summary>
/// Loads the caller's identity, enterprise memberships, and effective roles/permissions for
/// the currently-active enterprise. Delegates the projection to <see cref="IUserProfileBuilder"/>
/// so the same shape is shared with token-issuing endpoints (login/refresh/switch-enterprise).
/// </summary>
internal class GetMyProfileQueryHandler(
    ICurrentUserService currentUserService,
    IAppUserManager userManager,
    IUserProfileBuilder profileBuilder)
    : IRequestHandler<GetMyProfileQuery, Either<UserException, MyProfileInfo>>
{
    public async Task<Either<UserException, MyProfileInfo>> Handle(
        GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId is null) return new UserNotFoundException(Guid.Empty);

        var userOption = await userManager.GetUserByIdAsync(userId.Value);
        return await userOption.MatchAsync<User, Either<UserException, MyProfileInfo>>(
            Some: async user =>
            {
                var activeEnterpriseId = currentUserService.GetCurrentEnterpriseId();
                var profile = await profileBuilder.BuildAsync(user, activeEnterpriseId, cancellationToken);
                return profile;
            },
            None: () => new UserNotFoundException(userId.Value));
    }
}
