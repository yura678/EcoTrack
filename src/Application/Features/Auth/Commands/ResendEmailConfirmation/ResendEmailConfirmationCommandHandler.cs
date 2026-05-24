using Application.Common.EmailTemplates;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Auth.Commands.ResendEmailConfirmation;

internal class ResendEmailConfirmationCommandHandler(
    IAppUserManager userManager,
    IEmailService emailService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ResendEmailConfirmationCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(ResendEmailConfirmationCommand request,
        CancellationToken cancellationToken)
    {
        var userOption = await userManager.GetUserByEmail(request.Email);

        // Always return success to avoid leaking which emails exist.
        return await userOption.MatchAsync<User, Either<UserException, bool>>(
            Some: async u =>
            {
                if (u.EmailConfirmed) return true;

                var code = await userManager.GenerateEmailConfirmationCodeAsync(u);
                await emailService.SendEmailAsync(
                    toEmail: request.Email,
                    subject: "EcoTrack — confirm your email",
                    body: EmailTemplates.EmailConfirmation(code),
                    cancellationToken: cancellationToken);
                // Commit the email_outbox row so the confirmation email actually goes out.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            },
            None: () => true);
    }
}
