using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Application.Models.Profile;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Commands.UpdateMyProfile;

/// <summary>
/// Self-service edit of mutable profile fields. Email is NOT here — it doubles as the user's
/// login identifier and changing it goes through a separate confirmation flow (verify new
/// address via OTP before swap).
/// </summary>
internal class UpdateMyProfileCommandHandler(
    ICurrentUserService currentUserService,
    IAppUserManager userManager,
    IUserProfileBuilder profileBuilder,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateMyProfileCommand, Either<UserException, MyProfileInfo>>
{
    public async Task<Either<UserException, MyProfileInfo>> Handle(
        UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId is null) return new UserNotFoundException(Guid.Empty);

        var userOption = await userManager.GetUserByIdAsync(userId.Value);
        return await userOption.MatchAsync<User, Either<UserException, MyProfileInfo>>(
            Some: async user =>
            {
                user.Name = request.Name;
                user.FamilyName = request.FamilyName;
                await userManager.UpdateUserAsync(user);
                // Explicit save — defensive against the upcoming AutoSaveChanges=false on
                // AppUserStore. Today the Identity store auto-saves so this is a no-op.
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var activeEnterpriseId = currentUserService.GetCurrentEnterpriseId();
                return await profileBuilder.BuildAsync(user, activeEnterpriseId, cancellationToken);
            },
            None: () => new UserNotFoundException(userId.Value));
    }
}
