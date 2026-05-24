using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Shared.Extensions;

namespace Application.Features.Auth.Commands.ConfirmEmail;

internal class ConfirmEmailCommandHandler(
    IAppUserManager userManager,
    IUnitOfWork unitOfWork)
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
                if (!result.Succeeded)
                {
                    return new UserVerificationException(u.Id, result.Errors.StringifyIdentityResultErrors());
                }
                // AppUserStore.AutoSaveChanges is false, so ConfirmEmailWithCodeAsync only stages
                // EmailConfirmed + SecurityStamp + ConcurrencyStamp on the tracker; this commits.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            },
            None: () => new UserNotFoundException(Guid.Empty));
    }
}
