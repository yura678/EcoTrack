using Application.Common.Interfaces.Identity;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Shared.Extensions;

namespace Application.Features.Auth.Commands.ConfirmEmail;

internal class ConfirmEmailCommandHandler(IAppUserManager userManager)
    : IRequestHandler<ConfirmEmailCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(ConfirmEmailCommand request,
        CancellationToken cancellationToken)
    {
        var userOption = await userManager.GetUserByEmail(request.Email);
        return await userOption.MatchAsync<User, Either<UserException, bool>>(
            Some: async u =>
            {
                if (u.EmailConfirmed) return true;

                var result = await userManager.ConfirmEmailWithCodeAsync(u, request.Code);
                return result.Succeeded
                    ? true
                    : new UserVerificationException(u.Id, result.Errors.StringifyIdentityResultErrors());
            },
            None: () => new UserNotFoundException(Guid.Empty));
    }
}
