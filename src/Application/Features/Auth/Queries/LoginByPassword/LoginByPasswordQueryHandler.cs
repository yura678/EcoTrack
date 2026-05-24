using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Application.Models.Jwt;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Auth.Queries.LoginByPassword;

internal class LoginByPasswordQueryHandler(
    IAppUserManager userManager,
    IJwtService jwtService,
    ILoginAttemptRecorder loginRecorder,
    IEnterpriseAccessGate enterpriseAccessGate,
    IUnitOfWork unitOfWork,
    ILogger<LoginByPasswordQueryHandler> logger)
    : IRequestHandler<LoginByPasswordQuery, Either<UserException, AccessToken>>
{
    public async Task<Either<UserException, AccessToken>> Handle(LoginByPasswordQuery request,
        CancellationToken cancellationToken)
    {
        var userOption = await userManager.GetUserByEmail(request.Email);

        return await userOption.MatchAsync<User, Either<UserException, AccessToken>>(
            Some: async u =>
            {
                if (await userManager.IsUserLockedOutAsync(u))
                {
                    logger.LogWarning("Login attempt on locked user {UserId}", u.Id);
                    await loginRecorder.RecordAsync(u.Id, request.Email,
                        LoginMethod.Password, LoginOutcome.UserLocked, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return new InvalidCredentialsException(Guid.Empty);
                }

                if (!await userManager.IsPasswordValidAsync(u, request.Password))
                {
                    await userManager.IncrementAccessFailedCountAsync(u);
                    logger.LogWarning("Failed login attempt for user {UserId}", u.Id);
                    await loginRecorder.RecordAsync(u.Id, request.Email,
                        LoginMethod.Password, LoginOutcome.InvalidCredentials, cancellationToken);
                    // One save commits both the IncrementAccessFailedCount mutation on the User
                    // row and the LoginAttempt audit row.
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return new InvalidCredentialsException(Guid.Empty);
                }

                if (!u.EmailConfirmed)
                {
                    await loginRecorder.RecordAsync(u.Id, request.Email,
                        LoginMethod.Password, LoginOutcome.EmailNotConfirmed, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return new UserVerificationException(u.Id, "Email is not confirmed.");
                }

                // Enterprise approval gate: even with valid credentials, block JWT issuance
                // if the user's only tenants are Pending/Rejected. The lockout reset + the
                // login-success audit row above are intentionally kept — the user authenticated
                // correctly, just isn't authorised to enter their tenant yet.
                var gateBlock = await enterpriseAccessGate.CheckLoginEligibilityAsync(u.Id, cancellationToken);
                if (gateBlock.IsSome)
                {
                    await loginRecorder.RecordAsync(u.Id, request.Email,
                        LoginMethod.Password, LoginOutcome.EnterpriseNotApproved, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return gateBlock.Match(ex => ex, () => (UserException)new UnhandledUserException(u.Id));
                }

                await userManager.ResetUserLockoutAsync(u);
                await loginRecorder.RecordAsync(u.Id, request.Email,
                    LoginMethod.Password, LoginOutcome.Success, cancellationToken);
                // One save commits the cleared lockout/access-failed counters on the User row
                // alongside the success LoginAttempt audit row.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return await jwtService.GenerateAsync(u, null, cancellationToken);
            },
            None: async () =>
            {
                logger.LogWarning("Login attempt with unknown email");
                await loginRecorder.RecordAsync(userId: null, request.Email,
                    LoginMethod.Password, LoginOutcome.UnknownEmail, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new InvalidCredentialsException(Guid.Empty);
            });
    }
}
