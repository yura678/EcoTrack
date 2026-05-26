using Application.Common.Interfaces.Auditing;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.Auditing;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.RestoreMembership;

/// <summary>
/// Re-activates a previously-revoked membership. Tenant admin acts within their own
/// enterprise (CompanyId claim); SuperAdmin acts cross-tenant by passing
/// <see cref="RestoreMembershipCommand.EnterpriseId"/>. Idempotent — restoring an already-
/// active membership succeeds silently. The previous RoleId is reused; if the admin wants a
/// different role they can follow up with ChangeMemberRole.
/// </summary>
internal class RestoreMembershipCommandHandler(
    IUnitOfWork unitOfWork,
    IAppUserManager userManager,
    ICurrentUserService currentUserService,
    IAdminAuditService adminAudit)
    : IRequestHandler<RestoreMembershipCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(
        RestoreMembershipCommand request, CancellationToken cancellationToken)
    {
        var isSuperAdmin = currentUserService.IsSuperAdmin();
        var enterpriseId = isSuperAdmin
            ? request.EnterpriseId
            : currentUserService.GetCurrentEnterpriseId();
        if (enterpriseId is null)
            return new EnterpriseNotFound(Guid.Empty);

        var membershipOption = await unitOfWork.UserEnterpriseMembershipRepository
            .GetByUserAndEnterpriseAsync(request.UserId, enterpriseId.Value, cancellationToken);

        return await membershipOption.MatchAsync<UserEnterpriseMembership, Either<UserException, bool>>(
            Some: async membership =>
            {
                // Idempotent: already active rows succeed without rewriting state or emitting
                // an audit row. Avoids duplicate "Membership restored" entries when the admin
                // double-clicks or re-runs a script.
                if (membership.IsActive) return true;

                membership.Reinstate(membership.RoleId);
                unitOfWork.UserEnterpriseMembershipRepository.Update(membership);

                var userOption = await userManager.GetUserByIdAsync(request.UserId);
                var targetEmail = userOption.Match(u => u.Email, () => (string?)null);

                await adminAudit.LogAsync(
                    action: AuditAction.UserMembershipRestored,
                    targetType: AuditTargetType.User,
                    targetId: request.UserId,
                    targetLabel: targetEmail,
                    enterpriseId: enterpriseId,
                    details: null,
                    cancellationToken: cancellationToken);

                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            },
            None: () => new UserNotFoundException(request.UserId));
    }
}
