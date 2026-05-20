using System.Text.Json;
using Application.Common.EmailTemplates;
using Application.Common.Interfaces.Auditing;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.Auditing;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.ForcePasswordReset;

/// <summary>
/// Admin-initiated password reset. The admin presses "force password reset" in the user
/// profile; we generate a reset token, send the same email a self-service flow would send,
/// and log an <see cref="AuditAction.UserPasswordResetRequested"/> row immediately so the
/// admin has feedback even if the user never clicks the link. Completion later lands as
/// <see cref="AuditAction.UserPasswordResetCompleted"/> from
/// <see cref="Application.Features.Auth.Commands.ResetPassword.ResetPasswordCommandHandler"/>.
///
/// Tenant scoping: admin can only force-reset users who are members of the admin's current
/// enterprise — a separate query against UserEnterpriseMembership confirms this before any
/// token leaves the system. Pure superAdmin actions (no enterprise context) fall through and
/// can target any user.
/// </summary>
internal class ForcePasswordResetCommandHandler(
    IUnitOfWork unitOfWork,
    IAppUserManager userManager,
    ICurrentUserService currentUserService,
    IEmailService emailService,
    IAdminAuditService adminAudit)
    : IRequestHandler<ForcePasswordResetCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(
        ForcePasswordResetCommand request, CancellationToken cancellationToken)
    {
        var enterpriseId = currentUserService.GetCurrentEnterpriseId();
        var isSuperAdmin = currentUserService.IsSuperAdmin();

        // Non-superAdmin must be acting within a tenant context.
        if (!isSuperAdmin && enterpriseId is null)
            return new EnterpriseNotFound(Guid.Empty);

        var userOption = await userManager.GetUserByIdAsync(request.UserId);
        return await userOption.MatchAsync<User, Either<UserException, bool>>(
            Some: async user =>
            {
                // Verify the target user belongs to the admin's enterprise (skip for superAdmin).
                if (!isSuperAdmin)
                {
                    var membershipOption = await unitOfWork.UserEnterpriseMembershipRepository
                        .GetByUserAndEnterpriseAsync(user.Id, enterpriseId!.Value, cancellationToken);
                    var isActiveMember = membershipOption.Match(
                        Some: m => m.IsActive,
                        None: () => false);
                    if (!isActiveMember)
                        return new UserNotFoundException(request.UserId);
                }

                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var resetLink =
                    $"https://ecotrack.com/reset-password?email={Uri.EscapeDataString(user.Email!)}" +
                    $"&token={Uri.EscapeDataString(token)}";
                await emailService.SendEmailAsync(
                    toEmail: user.Email!,
                    subject: "Password reset initiated by your administrator",
                    body: EmailTemplates.PasswordResetByEmail(resetLink),
                    cancellationToken: cancellationToken);

                var details = JsonSerializer.SerializeToDocument(new
                {
                    initiatedBy = "admin",
                    targetEmail = user.Email
                });
                await adminAudit.LogAsync(
                    action: AuditAction.UserPasswordResetRequested,
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
