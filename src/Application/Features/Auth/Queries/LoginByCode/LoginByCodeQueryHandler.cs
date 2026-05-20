using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Features.Users.Exceptions;
using Application.Models.Jwt;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Shared.Extensions;

namespace Application.Features.Auth.Queries.LoginByCode;

internal class LoginByCodeQueryHandler(
    IAppUserManager userManager,
    IJwtService jwtService,
    ILoginAttemptRecorder loginRecorder)
    : IRequestHandler<LoginByCodeQuery, Either<UserException, AccessToken>>
{
    public async Task<Either<UserException, AccessToken>> Handle(LoginByCodeQuery request,
        CancellationToken cancellationToken)
    {
        var userOption = await userManager.GetUserByEmail(request.Email);

        return await userOption.MatchAsync<User, Either<UserException, AccessToken>>(
            Some: async u =>
            {
                if (!u.EmailConfirmed)
                {
                    await loginRecorder.RecordAsync(u.Id, request.Email,
                        LoginMethod.EmailCode, LoginOutcome.EmailNotConfirmed, cancellationToken);
                    return new UserVerificationException(u.Id, "Email is not confirmed.");
                }

                var result = await userManager.VerifyUserCode(u, request.Code);
                if (!result.Succeeded)
                {
                    await loginRecorder.RecordAsync(u.Id, request.Email,
                        LoginMethod.EmailCode, LoginOutcome.InvalidCredentials, cancellationToken);
                    return new UserVerificationException(u.Id, result.Errors.StringifyIdentityResultErrors());
                }

                await loginRecorder.RecordAsync(u.Id, request.Email,
                    LoginMethod.EmailCode, LoginOutcome.Success, cancellationToken);
                return await jwtService.GenerateAsync(u, null, cancellationToken);
            },
            None: async () =>
            {
                await loginRecorder.RecordAsync(userId: null, request.Email,
                    LoginMethod.EmailCode, LoginOutcome.UnknownEmail, cancellationToken);
                return new InvalidCredentialsException(Guid.Empty);
            });
    }
}
