using Application.Common.Interfaces;
using Application.Features.Users.Exceptions;
using Application.Models.Common;
using Application.Models.Jwt;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.RefreshUserTokenCommand
{
    internal class RefreshUserTokenCommandHandler(IJwtService jwtService)
        : IRequestHandler<RefreshUserTokenCommand, Either<UserException, AccessToken>>
    {
        public async Task<Either<UserException, AccessToken>> Handle(
            RefreshUserTokenCommand request,
            CancellationToken cancellationToken)
        {
            var newToken = await jwtService.RefreshToken(request.RefreshToken, cancellationToken);

            return newToken.Match<Either<UserException, AccessToken>>(
                Some: token => token,
                None: () => new InvalidRefreshTokenException(Guid.Empty)
            );
            
            
            
        }
    }
}