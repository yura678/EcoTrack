using Application.Common.EmailTemplates;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Auth.Queries.RequestLoginCode;

internal class RequestLoginCodeQueryHandler(
    IAppUserManager userManager,
    IEmailService emailService)
    : IRequestHandler<RequestLoginCodeQuery, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(RequestLoginCodeQuery request,
        CancellationToken cancellationToken)
    {
        var userOption = await userManager.GetUserByEmail(request.Email);

        // Always return success to avoid enumeration. Send only to confirmed accounts.
        return await userOption.MatchAsync<User, Either<UserException, bool>>(
            Some: async u =>
            {
                if (!u.EmailConfirmed) return true;

                var code = await userManager.GenerateOtpCode(u);
                await emailService.SendEmailAsync(
                    toEmail: request.Email,
                    subject: "EcoTrack — your login code",
                    body: EmailTemplates.LoginCode(code),
                    cancellationToken: cancellationToken);
                return true;
            },
            None: () => true);
    }
}
