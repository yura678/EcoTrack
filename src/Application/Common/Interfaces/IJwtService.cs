using System.Security.Claims;
using Application.Models.Jwt;
using Domain.Entities.User;
using LanguageExt;

namespace Application.Common.Interfaces;

public interface IJwtService
{
    Task<TokenIssueResult> GenerateAsync(User user, Guid? enterpriseId, CancellationToken cancellationToken);
    Task<ClaimsPrincipal> GetPrincipalFromExpiredToken(string token);
    Task<TokenIssueResult> GenerateByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);
    Task<Option<(TokenIssueResult Issued, User User)>> RefreshToken(Guid refreshTokenId, CancellationToken cancellationToken);
}
