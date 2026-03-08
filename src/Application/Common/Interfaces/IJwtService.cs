using System.Security.Claims;
using Application.Models.Jwt;
using Domain.Entities.User;
using LanguageExt;

namespace Application.Common.Interfaces;

public interface IJwtService
{
    Task<AccessToken> GenerateAsync(User user, CancellationToken cancellationToken);
    Task<ClaimsPrincipal> GetPrincipalFromExpiredToken(string token);
    Task<AccessToken> GenerateByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);
    Task<Option<AccessToken>> RefreshToken(Guid refreshTokenId, CancellationToken cancellationToken);
}