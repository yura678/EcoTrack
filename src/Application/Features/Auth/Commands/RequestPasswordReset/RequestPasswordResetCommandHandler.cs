using Application.Common.EmailTemplates;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Common.Settings;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Auth.Commands.RequestPasswordReset;

/// <summary>
/// Anonymous "forgot password" entry point. Always returns Unit regardless of whether the
/// email exists in the system — this is deliberate anti-enumeration: a 200 either way denies
/// attackers the ability to probe which addresses have accounts. If the email is unknown
/// we still log a structured debug message so legitimate operators can investigate by
/// correlating logs with the request, but no email is sent.
/// </summary>
internal class RequestPasswordResetCommandHandler(
    IAppUserManager userManager,
    IEmailService emailService,
    IUnitOfWork unitOfWork,
    FrontendSettings frontendSettings,
    ILogger<RequestPasswordResetCommandHandler> logger)
    : IRequestHandler<RequestPasswordResetCommand, Unit>
{
    public async Task<Unit> Handle(
        RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var userOption = await userManager.GetUserByEmail(request.Email);
        await userOption.MatchAsync(
            Some: async user =>
            {
                // EmailConfirmed gate matches login behaviour — unconfirmed users can't reset
                // via this flow; they should confirm first (or use email-code login which sets
                // the flag as part of registration).
                if (!user.EmailConfirmed)
                {
                    logger.LogDebug(
                        "Password reset requested for unconfirmed email {Email}; skipping send",
                        request.Email);
                    return 0;
                }

                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var resetLink =
                    $"{frontendSettings.BaseUrl}/reset-password?email={Uri.EscapeDataString(user.Email!)}" +
                    $"&token={Uri.EscapeDataString(token)}";
                await emailService.SendEmailAsync(
                    toEmail: user.Email!,
                    subject: "Reset your EcoTrack password",
                    body: EmailTemplates.PasswordResetByEmail(resetLink),
                    cancellationToken: cancellationToken);
                // Commit the email_outbox row. Without this the outbox stays in the tracker
                // forever and the password-reset link never reaches the user.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return 0;
            },
            None: () =>
            {
                logger.LogDebug(
                    "Password reset requested for unknown email {Email}; no email sent",
                    request.Email);
                return Task.FromResult(0);
            });

        return Unit.Value;
    }
}
