using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Features.Users.Exceptions;
using Application.Models.Auth;
using Application.Models.Jwt;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.RefreshUserTokenCommand
{
    internal class RefreshUserTokenCommandHandler(
        IJwtService jwtService,
        IUserProfileBuilder profileBuilder)
        : IRequestHandler<RefreshUserTokenCommand, Either<UserException, AuthSession>>
    {
        public async Task<Either<UserException, AuthSession>> Handle(
            RefreshUserTokenCommand request,
            CancellationToken cancellationToken)
        {
            var refreshed = await jwtService.RefreshToken(request.RefreshToken, cancellationToken);

            return await refreshed.MatchAsync<(TokenIssueResult Issued, Domain.Entities.User.User User), Either<UserException, AuthSession>>(
                Some: async pair =>
                {
                    var profile = await profileBuilder.BuildAsync(
                        pair.User, pair.Issued.ResolvedEnterpriseId, cancellationToken);
                    return new AuthSession(pair.Issued.Token, profile);
                },
                None: () => new InvalidRefreshTokenException(Guid.Empty)
            );
        }
    }
}
