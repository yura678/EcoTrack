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

namespace Application.Features.Auth.Commands.ResetPassword;

/// <summary>
/// Consumes a password reset token issued by <see cref="RequestPasswordReset.RequestPasswordResetCommand"/>
/// (self-service) or by the admin force-reset endpoint. On success:
/// 1. Identity rotates the security stamp, invalidating the consumed token + any other live one.
/// 2. We invalidate all of the user's refresh tokens so active sessions are forced to re-login.
/// 3. An <see cref="AuditAction.UserPasswordResetCompleted"/> row is written attributing the
///    action to the user themselves (the request is unauthenticated; the actor here is the
///    user who proved control of the email by submitting a valid token).
///
/// Audit is written only on success — failed attempts (bad token, weak password) would just
/// add noise to the timeline.
/// </summary>
internal class ResetPasswordCommandHandler(
    IAppUserManager userManager,
    IUnitOfWork unitOfWork,
    IAdminAuditService adminAudit)
    : IRequestHandler<ResetPasswordCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(
        ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var userOption = await userManager.GetUserByEmail(request.Email);
        return await userOption.MatchAsync<User, Either<UserException, bool>>(
            Some: async user =>
            {
                var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
                if (!result.Succeeded)
                {
                    return new UserVerificationException(user.Id, result.Errors.StringifyIdentityResultErrors());
                }

                await unitOfWork.UserRefreshTokenRepository.InvalidateAllForUserAsync(
                    user.Id, cancellationToken);

                var details = JsonSerializer.SerializeToDocument(new
                {
                    completedBy = "self-service",
                    sessionsInvalidated = true
                });
                await adminAudit.LogWithExplicitActorAsync(
                    actorUserId: user.Id,
                    actorEmail: user.Email ?? "<unknown>",
                    action: AuditAction.UserPasswordResetCompleted,
                    targetType: AuditTargetType.User,
                    targetId: user.Id,
                    targetLabel: user.Email,
                    enterpriseId: null,
                    details: details,
                    cancellationToken: cancellationToken);

                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            },
            // Same anti-enumeration shape as the request endpoint — leak nothing about whether
            // the email exists. The token check would fail anyway, so this just normalises the
            // error message.
            None: () => new UserVerificationException(Guid.Empty, "Invalid token or email."));
    }
}
