using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Queries;
using Application.Features.Users.Exceptions;
using Application.Models.Auth;
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
    IUserEnterpriseMembershipQueries membershipQueries,
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

                var memberships = await membershipQueries
                    .GetByUserIdWithRoleAndEnterpriseAsync(user.Id, cancellationToken);
                var membershipInfo = memberships.Select(m => new MembershipInfo(
                    m.EnterpriseId,
                    m.Enterprise?.Name ?? string.Empty,
                    m.RoleId,
                    m.Role?.Name ?? string.Empty,
                    m.Role?.DisplayName,
                    m.JoinedAt)).ToList();

                return new MyProfileInfo(
                    UserId: user.Id,
                    Email: user.Email ?? string.Empty,
                    EmailConfirmed: user.EmailConfirmed,
                    Name: user.Name,
                    FamilyName: user.FamilyName,
                    Memberships: membershipInfo);
            },
            None: () => new UserNotFoundException(userId.Value));
    }
}
