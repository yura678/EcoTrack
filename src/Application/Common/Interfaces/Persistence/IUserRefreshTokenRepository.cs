using Domain.Entities.User;
using LanguageExt;

namespace Application.Common.Interfaces.Persistence;

public interface IUserRefreshTokenRepository
{
    Task<Guid> CreateToken(Guid userId, Guid? enterpriseId, DateTime expiresAt, CancellationToken cancellationToken);
    Task<Option<UserRefreshToken>> GetTokenWithInvalidation(Guid id);
    Task<Option<User>> GetUserByRefreshToken(Guid tokenId);
    Task InvalidateAllForUserAsync(Guid userId, CancellationToken cancellationToken);
}
