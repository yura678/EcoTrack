using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries;
using Application.Features.Users.Exceptions;
using Application.Models.Auth;
using Application.Models.Profile;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Queries.GetMyProfile;

/// <summary>
/// Loads the caller's identity plus their enterprise memberships. No tenant filtering — by
/// definition the caller is asking about themselves, and a multi-tenant user must see all
/// their memberships to know what to switch into.
/// </summary>
internal class GetMyProfileQueryHandler(
    ICurrentUserService currentUserService,
    IAppUserManager userManager,
    IUserEnterpriseMembershipQueries membershipQueries)
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
