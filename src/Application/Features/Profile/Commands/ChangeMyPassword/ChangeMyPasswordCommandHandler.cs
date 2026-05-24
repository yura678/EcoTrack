using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Shared.Extensions;

namespace Application.Features.Profile.Commands.ChangeMyPassword;

/// <summary>
/// Authenticated self-service password change. ASP.NET Identity verifies the current password
/// before applying the new one and rotates the user's security stamp on success.
///
/// We do NOT invalidate refresh tokens here — the current device that just changed the
/// password is still trusted. If "log out other devices" semantics are needed, the user can
/// hit the upcoming /profile/sessions endpoints to revoke specific sessions.
/// </summary>
internal class ChangeMyPasswordCommandHandler(
    ICurrentUserService currentUserService,
    IAppUserManager userManager,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ChangeMyPasswordCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(
        ChangeMyPasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId is null) return new UserNotFoundException(Guid.Empty);

        var userOption = await userManager.GetUserByIdAsync(userId.Value);
        return await userOption.MatchAsync<User, Either<UserException, bool>>(
            Some: async user =>
            {
                var result = await userManager.ChangePasswordAsync(
                    user, request.CurrentPassword, request.NewPassword);
                if (!result.Succeeded)
                {
                    return new UserVerificationException(user.Id, result.Errors.StringifyIdentityResultErrors());
                }
                // Explicit save — defensive against the upcoming AutoSaveChanges=false on
                // AppUserStore. Today the Identity store auto-saves so this is a no-op; once
                // AutoSaveChanges flips this is the only way the password hash + security stamp
                // reach the DB.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            },
            None: () => new UserNotFoundException(userId.Value));
    }
}
