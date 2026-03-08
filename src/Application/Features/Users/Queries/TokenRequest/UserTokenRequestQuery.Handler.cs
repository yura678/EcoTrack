using Application.Common.EmailTemplates;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Users.Queries.TokenRequest;

public class UserTokenRequestQueryHandler(
    IAppUserManager userManager,
    IEmailService emailService,
    ILogger<UserTokenRequestQueryHandler> logger)
    : IRequestHandler<UserTokenRequestQuery, Either<UserException, UserTokenRequestQueryResponse>>
{
    public async Task<Either<UserException, UserTokenRequestQueryResponse>> Handle(UserTokenRequestQuery request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserByEmail(request.Email);


        return await user.MatchAsync<User, Either<UserException, UserTokenRequestQueryResponse>>(
            async u =>
            {
                var code = u.EmailConfirmed
                    ? await userManager.GenerateOtpCode(u)
                    : await userManager.GenerateEmailConfirmationToken(u, u.Email);

                logger.LogWarning($"Generated Code for user Id {u.Id} is {code}");
                
                var emailBody = u.EmailConfirmed
                    ? EmailTemplates.LoginCode(code)
                    : EmailTemplates.EmailConfirmation(code);

                await emailService.SendEmailAsync(
                    toEmail: request.Email,
                    subject: "EcoTrack",
                    body: emailBody,
                    cancellationToken: cancellationToken
                );

                return new UserTokenRequestQueryResponse { UserKey = u.GeneratedCode };
            },
            () => new UserNotFoundException(Guid.Empty)
        );
    }
}