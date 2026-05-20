using System.Text.Json;
using Application.Common.Interfaces.Auditing;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.Auditing;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Shared.Extensions;

namespace Application.Features.Users.Commands.ChangeUserEmail;

/// <summary>
/// Admin-initiated email change. Same tenant scoping as user-detail: admin can act only on
/// users with an active membership in their enterprise; superAdmin bypasses. On success the
/// target user's email + username flip in one transaction, refresh tokens for the admin's
/// tenant are invalidated (force re-login with the new address), and an audit row is written
/// recording old → new.
/// </summary>
internal class ChangeUserEmailCommandHandler(
    IUnitOfWork unitOfWork,
    IAppUserManager userManager,
    ICurrentUserService currentUserService,
    IAdminAuditService adminAudit)
    : IRequestHandler<ChangeUserEmailCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(
        ChangeUserEmailCommand request, CancellationToken cancellationToken)
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

                // Normalize equality check — admins who type the same address with different
                // casing shouldn't get a confusing 409.
                if (string.Equals(user.Email, request.NewEmail, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (await userManager.IsExistEmail(request.NewEmail))
                    return new EmailAlreadyExistsException(request.UserId);

                var oldEmail = user.Email;
                var result = await userManager.AdminChangeEmailAsync(user, request.NewEmail);
                if (!result.Succeeded)
                    return new UserVerificationException(user.Id,
                        result.Errors.StringifyIdentityResultErrors());

                // Refresh tokens for the current tenant die so existing JWTs (which carry the
                // old email in claims) cannot be silently refreshed. Cross-tenant tokens stay
                // alive — the email change applies globally to the User row, but admin of A
                // has no authority to log the user out of tenant B's sessions.
                if (enterpriseId.HasValue)
                {
                    await unitOfWork.UserRefreshTokenRepository
                        .InvalidateAllForUserAndEnterpriseAsync(
                            user.Id, enterpriseId.Value, cancellationToken);
                }

                var details = JsonSerializer.SerializeToDocument(new
                {
                    oldEmail,
                    newEmail = request.NewEmail
                });
                await adminAudit.LogAsync(
                    action: AuditAction.UserEmailChanged,
                    targetType: AuditTargetType.User,
                    targetId: user.Id,
                    targetLabel: request.NewEmail,
                    enterpriseId: enterpriseId,
                    details: details,
                    cancellationToken: cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return true;
            },
            None: () => new UserNotFoundException(request.UserId));
    }
}
