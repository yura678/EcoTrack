using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.ChangeMemberRole;

internal class ChangeMemberRoleCommandHandler(
    IUnitOfWork unitOfWork,
    IAppUserManager userManager,
    IRoleManagerService roleManagerService,
    ICurrentUserService currentUserService)
    : IRequestHandler<ChangeMemberRoleCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(ChangeMemberRoleCommand request,
        CancellationToken cancellationToken)
    {
        var enterpriseId = currentUserService.GetCurrentEnterpriseId();
        if (enterpriseId is null)
            return new EnterpriseNotFound(Guid.Empty);

        var roleOption = await roleManagerService.GetRoleByIdAsync(request.RoleId);
        var roleBelongsToTenant = roleOption.Match(
            Some: r => r.EnterpriseId == enterpriseId,
            None: () => false);
        if (!roleBelongsToTenant)
            return new UserRoleNotFoundException(Guid.Empty, request.RoleId);

        var membershipOption = await unitOfWork.UserEnterpriseMembershipRepository
            .GetByUserAndEnterpriseAsync(request.UserId, enterpriseId.Value, cancellationToken);

        return await membershipOption.MatchAsync<UserEnterpriseMembership, Either<UserException, bool>>(
            Some: async membership =>
            {
                if (!membership.IsActive)
                    return new UserNotFoundException(request.UserId);

                membership.ChangeRole(request.RoleId);
                unitOfWork.UserEnterpriseMembershipRepository.Update(membership);

                // Force re-login for this tenant so the new role takes effect immediately
                // instead of after JWT natural expiration. Sessions in other enterprises stay
                // valid — role change in one tenant must not log the user out of others.
                await unitOfWork.UserRefreshTokenRepository.InvalidateAllForUserAndEnterpriseAsync(
                    request.UserId, enterpriseId.Value, cancellationToken);

                await unitOfWork.SaveChangesAsync(cancellationToken);

                await RotateSecurityStampAsync(request.UserId);
                // Explicit save — UpdateSecurityStampAsync only tracks the new stamp once
                // AutoSaveChanges is off on AppUserStore; without this flush the stamp change
                // never reaches the DB and existing JWTs keep working as if the role hadn't
                // changed.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            },
            None: () => new UserNotFoundException(request.UserId));
    }

    private async Task RotateSecurityStampAsync(Guid userId)
    {
        var userOption = await userManager.GetUserByIdAsync(userId);
        await userOption.IfSomeAsync(u => userManager.UpdateSecurityStampAsync(u));
    }
}
