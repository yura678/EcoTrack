using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries;
using Application.Features.Users.Exceptions;
using Application.Models.Auth;
using Application.Models.Users;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Queries.GetUserDetail;

/// <summary>
/// Admin view of a single user. Tenant-scoping rule:
///   * admin sees user iff there is an active membership in the admin's current enterprise;
///   * superAdmin can see anyone — Membership is null when the user has no row in the
///     admin's (possibly empty) enterprise context.
/// Returning UserNotFoundException for a user that exists but is outside the admin's tenant
/// is intentional anti-enumeration: an admin of enterprise A should not be able to probe
/// which user ids exist in enterprise B.
/// </summary>
internal class GetUserDetailQueryHandler(
    ICurrentUserService currentUserService,
    IAppUserManager userManager,
    IUserEnterpriseMembershipQueries membershipQueries)
    : IRequestHandler<GetUserDetailQuery, Either<UserException, UserDetailInfo>>
{
    public async Task<Either<UserException, UserDetailInfo>> Handle(
        GetUserDetailQuery request, CancellationToken cancellationToken)
    {
        var isSuperAdmin = currentUserService.IsSuperAdmin();
        var enterpriseId = currentUserService.GetCurrentEnterpriseId();
        if (!isSuperAdmin && enterpriseId is null)
            return new EnterpriseNotFound(Guid.Empty);

        var userOption = await userManager.GetUserByIdAsync(request.UserId);
        return await userOption.MatchAsync<User, Either<UserException, UserDetailInfo>>(
            Some: async user =>
            {
                MembershipInfo? membershipInfo = null;
                if (enterpriseId.HasValue)
                {
                    var membershipOption = await membershipQueries
                        .GetActiveByUserAndEnterpriseWithRoleAsync(
                            user.Id, enterpriseId.Value, cancellationToken);
                    // IfSome (vs Match returning null) — LanguageExt's Match treats a null
                    // return from either branch as ResultIsNullException; we keep nullable
                    // semantics via the side-effecting IfSome instead.
                    membershipOption.IfSome(m =>
                    {
                        membershipInfo = new MembershipInfo(
                            m.EnterpriseId,
                            m.Enterprise?.Name ?? string.Empty,
                            m.RoleId,
                            m.Role?.Name ?? string.Empty,
                            m.Role?.DisplayName,
                            m.JoinedAt);
                    });
                }

                // For a tenant-scoped admin, no active membership in the current enterprise =
                // user is invisible. Don't leak existence across tenants.
                if (!isSuperAdmin && membershipInfo is null)
                    return new UserNotFoundException(request.UserId);

                var isLocked = await userManager.IsUserLockedOutAsync(user);
                return new UserDetailInfo(
                    UserId: user.Id,
                    Email: user.Email ?? string.Empty,
                    EmailConfirmed: user.EmailConfirmed,
                    Name: user.Name,
                    FamilyName: user.FamilyName,
                    LockoutEnd: user.LockoutEnd,
                    IsLockedOut: isLocked,
                    AccessFailedCount: user.AccessFailedCount,
                    Membership: membershipInfo);
            },
            None: () => new UserNotFoundException(request.UserId));
    }
}
