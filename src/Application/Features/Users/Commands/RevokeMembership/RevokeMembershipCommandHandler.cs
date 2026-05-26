using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.RevokeMembership;

internal class RevokeMembershipCommandHandler(
    IUnitOfWork unitOfWork,
    IAppUserManager userManager,
    ICurrentUserService currentUserService)
    : IRequestHandler<RevokeMembershipCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(RevokeMembershipCommand request,
        CancellationToken cancellationToken)
    {
        // SuperAdmin acts cross-tenant via request.EnterpriseId; regular admin always uses
        // the JWT's CompanyId claim. See ChangeMemberRoleCommandHandler for the same pattern
        // and rationale (avoid letting tenant A's admin revoke memberships in tenant B).
        var isSuperAdmin = currentUserService.IsSuperAdmin();
        var enterpriseId = isSuperAdmin
            ? request.EnterpriseId
            : currentUserService.GetCurrentEnterpriseId();
        if (enterpriseId is null)
            return new EnterpriseNotFound(Guid.Empty);

        if (request.UserId == currentUserService.GetCurrentUserId())
            return new UserVerificationException(request.UserId,
                "Admin cannot revoke their own membership; transfer admin rights first");

        var membershipOption = await unitOfWork.UserEnterpriseMembershipRepository
            .GetByUserAndEnterpriseAsync(request.UserId, enterpriseId.Value, cancellationToken);

        return await membershipOption.MatchAsync<UserEnterpriseMembership, Either<UserException, bool>>(
            Some: async membership =>
            {
                if (!membership.IsActive)
                    return new UserNotFoundException(request.UserId);

                membership.Revoke();
                unitOfWork.UserEnterpriseMembershipRepository.Update(membership);

                // Kill any live sessions this user has for THIS enterprise so the revoked
                // membership takes effect on the next API call instead of riding the existing
                // JWT until natural expiration. Sessions for other enterprises stay valid —
                // revoking from one tenant must not log the user out of unrelated tenants.
                await unitOfWork.UserRefreshTokenRepository.InvalidateAllForUserAndEnterpriseAsync(
                    request.UserId, enterpriseId.Value, cancellationToken);

                await unitOfWork.SaveChangesAsync(cancellationToken);

                var userOption = await userManager.GetUserByIdAsync(request.UserId);
                await userOption.IfSomeAsync(u => userManager.UpdateSecurityStampAsync(u));
                // Explicit save so the rotated stamp reaches the DB once AutoSaveChanges flips
                // off on AppUserStore. Without it the revoked user's existing JWTs continue
                // validating because the stamp on the User row never changed.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            },
            None: () => new UserNotFoundException(request.UserId));
    }
}
