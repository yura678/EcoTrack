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
        var role = roleOption.Match(r => r, () => null!);
        if (role is null || role.EnterpriseId != enterpriseId)
            return new UserRoleNotFoundException(Guid.Empty, request.RoleId);

        var membershipOption = await unitOfWork.UserEnterpriseMembershipRepository
            .GetByUserAndEnterpriseAsync(request.UserId, enterpriseId.Value, cancellationToken);
        var membership = membershipOption.Match(m => m, () => null!);
        if (membership is null || !membership.IsActive)
            return new UserNotFoundException(request.UserId);

        membership.ChangeRole(request.RoleId);
        unitOfWork.UserEnterpriseMembershipRepository.Update(membership);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Force re-login so the changed role takes effect.
        var userOption = await userManager.GetUserByIdAsync(request.UserId);
        await userOption.MatchAsync<User, bool>(
            Some: async u => { await userManager.UpdateSecurityStampAsync(u); return true; },
            None: () => false);

        return true;
    }
}
