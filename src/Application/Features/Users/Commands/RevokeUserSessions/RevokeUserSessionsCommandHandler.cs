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
/// Admin-initiated "log out everywhere in tenant X" for a target user. Tenant admin acts only
/// within their own enterprise (CompanyId claim). SuperAdmin acts cross-tenant by passing
/// request.EnterpriseId — they pick which tenant's sessions to kill, instead of a
/// blanket "everywhere" so the audit row still anchors to a single enterprise.
/// Sessions in other tenants always stay live: revoking from one tenant must not log the
/// user out of unrelated tenants.
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
        var isSuperAdmin = currentUserService.IsSuperAdmin();
        var enterpriseId = isSuperAdmin
            ? request.EnterpriseId
            : currentUserService.GetCurrentEnterpriseId();
        if (enterpriseId is null)
            return new EnterpriseNotFound(Guid.Empty);

        var userOption = await userManager.GetUserByIdAsync(request.UserId);
        return await userOption.MatchAsync<User, Either<UserException, bool>>(
            Some: async user =>
            {
                var membershipOption = await unitOfWork.UserEnterpriseMembershipRepository
                    .GetByUserAndEnterpriseAsync(user.Id, enterpriseId.Value, cancellationToken);
                var isMember = membershipOption.Match(
                    Some: m => m.IsActive,
                    None: () => false);
                if (!isMember) return new UserNotFoundException(request.UserId);

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
