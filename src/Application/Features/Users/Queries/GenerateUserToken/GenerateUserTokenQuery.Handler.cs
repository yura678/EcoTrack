using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Features.Users.Exceptions;
using Application.Models.Jwt;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Shared.Extensions;

namespace Application.Features.Users.Queries.GenerateUserToken;

internal class GenerateUserTokenQueryHandler(IJwtService jwtService, IAppUserManager userManager)
    : IRequestHandler<GenerateUserTokenQuery, Either<UserException, AccessToken>>
{
    public async Task<Either<UserException, AccessToken>> Handle(
        GenerateUserTokenQuery request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserByCode(request.UserKey);

        return await user.MatchAsync<User, Either<UserException, AccessToken>>(
            Some: async u =>
            {
                var result = u.EmailConfirmed
                    ? await userManager.VerifyUserCode(u, request.Code)
                    : await userManager.ChangeEmail(u, u.Email, request.Code);
                if (!result.Succeeded)
                    return new UserVerificationException(u.Id, result.Errors.StringifyIdentityResultErrors());

                await userManager.UpdateUserAsync(u);
                var token = await jwtService.GenerateAsync(u, cancellationToken);
                return token;
            },
            None: () => new UserNotFoundException(Guid.Empty)
        );
    }
}