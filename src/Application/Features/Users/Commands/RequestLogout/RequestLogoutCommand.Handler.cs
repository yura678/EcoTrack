using Application.Common.Interfaces.Identity;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.RequestLogout;

internal class RequestLogoutCommandHandler(IAppUserManager userManager)
    : IRequestHandler<RequestLogoutCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(
        RequestLogoutCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserByIdAsync(request.UserId);

        return await user.MatchAsync<User, Either<UserException, bool>>(
            Some: async u =>
            {
                await userManager.UpdateSecurityStampAsync(u);
                return true;
            },
            None: () => new UserNotFoundException(request.UserId)
        );
    }
}