using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Application.Models.Auth;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Shared.Extensions;

namespace Application.Features.Auth.Queries.LoginByCode;

internal class LoginByCodeQueryHandler(
    IAppUserManager userManager,
    IJwtService jwtService,
    IUserProfileBuilder profileBuilder,
    ILoginAttemptRecorder loginRecorder,
    IEnterpriseAccessGate enterpriseAccessGate,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LoginByCodeQuery, Either<UserException, AuthSession>>
{
    public async Task<Either<UserException, AuthSession>> Handle(LoginByCodeQuery request,
        CancellationToken cancellationToken)
    {
        var userOption = await userManager.GetUserByEmail(request.Email);

        return await userOption.MatchAsync<User, Either<UserException, AuthSession>>(
            Some: async u =>
            {
                if (!u.EmailConfirmed)
                {
                    await loginRecorder.RecordAsync(u.Id, request.Email,
                        LoginMethod.EmailCode, LoginOutcome.EmailNotConfirmed, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return new UserVerificationException(u.Id, "Email is not confirmed.");
                }

                var result = await userManager.VerifyUserCode(u, request.Code);
                if (!result.Succeeded)
                {
                    await loginRecorder.RecordAsync(u.Id, request.Email,
                        LoginMethod.EmailCode, LoginOutcome.InvalidCredentials, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return new UserVerificationException(u.Id, result.Errors.StringifyIdentityResultErrors());
                }

                // Enterprise approval gate: block JWT issuance when the user's only tenants
                // are Pending/Rejected. The security-stamp rotation from VerifyUserCode above
                // is intentionally retained — it's a credential-side mutation independent of
                // tenant access; we just don't issue a token until SuperAdmin approves.
                var gateBlock = await enterpriseAccessGate.CheckLoginEligibilityAsync(u.Id, cancellationToken);
                if (gateBlock.IsSome)
                {
                    await loginRecorder.RecordAsync(u.Id, request.Email,
                        LoginMethod.EmailCode, LoginOutcome.EnterpriseNotApproved, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return gateBlock.Match(ex => ex, () => (UserException)new UnhandledUserException(u.Id));
                }

                await loginRecorder.RecordAsync(u.Id, request.Email,
                    LoginMethod.EmailCode, LoginOutcome.Success, cancellationToken);
                // VerifyUserCode rotated the security stamp on the User row; one save commits
                // the stamp change alongside the success audit row.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                var issued = await jwtService.GenerateAsync(u, null, cancellationToken);
                var profile = await profileBuilder.BuildAsync(u, issued.ResolvedEnterpriseId, cancellationToken);
                return new AuthSession(issued.Token, profile);
            },
            None: async () =>
            {
                await loginRecorder.RecordAsync(userId: null, request.Email,
                    LoginMethod.EmailCode, LoginOutcome.UnknownEmail, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new InvalidCredentialsException(Guid.Empty);
            });
    }
}
