using System.Text.Json;
using Application.Common.Interfaces.Auditing;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.Auditing;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.UnlockUser;

/// <summary>
/// Admin-initiated unlock. Clears LockoutEnd + AccessFailedCount via UserManager so the user
/// can immediately log in again. Tenant scoping mirrors the user-detail query: admin can only
/// unlock users with an active membership in their enterprise; superAdmin can unlock anyone.
///
/// Idempotent — repeated calls on an already-unlocked user still succeed (and still emit an
/// audit row, since the intent of the click is what matters for the audit trail).
/// </summary>
internal class UnlockUserCommandHandler(
    IUnitOfWork unitOfWork,
    IAppUserManager userManager,
    ICurrentUserService currentUserService,
    IAdminAuditService adminAudit)
    : IRequestHandler<UnlockUserCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(
        UnlockUserCommand request, CancellationToken cancellationToken)
    {
        var isSuperAdmin = currentUserService.IsSuperAdmin();
        var enterpriseId = currentUserService.GetCurrentEnterpriseId();
        if (!isSuperAdmin && enterpriseId is null)
            return new EnterpriseNotFound(Guid.Empty);

        var userOption = await userManager.GetUserByIdAsync(request.UserId);
        return await userOption.MatchAsync<User, Either<UserException, bool>>(
            Some: async user =>
            {
                if (!isSuperAdmin)
                {
                    var membershipOption = await unitOfWork.UserEnterpriseMembershipRepository
                        .GetByUserAndEnterpriseAsync(user.Id, enterpriseId!.Value, cancellationToken);
                    var isMember = membershipOption.Match(
                        Some: m => m.IsActive,
                        None: () => false);
                    if (!isMember) return new UserNotFoundException(request.UserId);
                }

                var wasLocked = await userManager.IsUserLockedOutAsync(user);
                await userManager.ResetUserLockoutAsync(user);

                var details = JsonSerializer.SerializeToDocument(new { wasLocked });
                await adminAudit.LogAsync(
                    action: AuditAction.UserAccountUnlocked,
                    targetType: AuditTargetType.User,
                    targetId: user.Id,
                    targetLabel: user.Email,
                    enterpriseId: enterpriseId,
                    details: details,
                    cancellationToken: cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return true;
            },
            None: () => new UserNotFoundException(request.UserId));
    }
}
