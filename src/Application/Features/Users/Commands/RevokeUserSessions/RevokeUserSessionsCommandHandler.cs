using Application.Common.Interfaces.Auditing;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.Auditing;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.RevokeUserSessions;

/// <summary>
/// Admin-initiated "log out everywhere in MY tenant" for a target user. Reuses the per-tenant
/// invalidation primitive built for membership revoke (commit 73b1ee5) — sessions in OTHER
/// enterprises stay live, because an admin of A has no business kicking the user out of B.
/// SuperAdmin is intentionally not given a "kill all sessions everywhere" path here; if that's
/// ever needed it's a different endpoint with a stronger audit trail.
/// </summary>
internal class RevokeUserSessionsCommandHandler(
    IUnitOfWork unitOfWork,
    IAppUserManager userManager,
    ICurrentUserService currentUserService,
    IAdminAuditService adminAudit)
    : IRequestHandler<RevokeUserSessionsCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(
        RevokeUserSessionsCommand request, CancellationToken cancellationToken)
    {
        var enterpriseId = currentUserService.GetCurrentEnterpriseId();
        if (enterpriseId is null)
            return new EnterpriseNotFound(Guid.Empty);

        var userOption = await userManager.GetUserByIdAsync(request.UserId);
        return await userOption.MatchAsync<User, Either<UserException, bool>>(
            Some: async user =>
            {
                var isSuperAdmin = currentUserService.IsSuperAdmin();
                if (!isSuperAdmin)
                {
                    var membershipOption = await unitOfWork.UserEnterpriseMembershipRepository
                        .GetByUserAndEnterpriseAsync(user.Id, enterpriseId.Value, cancellationToken);
                    var isMember = membershipOption.Match(
                        Some: m => m.IsActive,
                        None: () => false);
                    if (!isMember) return new UserNotFoundException(request.UserId);
                }

                await unitOfWork.UserRefreshTokenRepository
                    .InvalidateAllForUserAndEnterpriseAsync(
                        user.Id, enterpriseId.Value, cancellationToken);

                await adminAudit.LogAsync(
                    action: AuditAction.UserSessionsRevoked,
                    targetType: AuditTargetType.User,
                    targetId: user.Id,
                    targetLabel: user.Email,
                    enterpriseId: enterpriseId,
                    details: null,
                    cancellationToken: cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return true;
            },
            None: () => new UserNotFoundException(request.UserId));
    }
}
