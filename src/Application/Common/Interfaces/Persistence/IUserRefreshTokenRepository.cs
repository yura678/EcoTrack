using Domain.Entities.User;
using LanguageExt;

namespace Application.Common.Interfaces.Persistence;

public interface IUserRefreshTokenRepository
{
    Task<Guid> CreateToken(Guid userId, Guid? enterpriseId, DateTime expiresAt, CancellationToken cancellationToken);
    Task<Option<UserRefreshToken>> GetTokenWithInvalidation(Guid id);
    Task<Option<User>> GetUserByRefreshToken(Guid tokenId);
    Task InvalidateAllForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Flips IsValid=false on every live refresh token issued for the (user, enterprise) pair.
    /// Used when revoking a membership or changing a role within one enterprise — the affected
    /// sessions die immediately while the user's sessions in OTHER enterprises keep working.
    /// Rows with NULL EnterpriseId (legacy or pre-multitenancy tokens) are not touched.
    /// </summary>
    Task InvalidateAllForUserAndEnterpriseAsync(
        Guid userId, Guid enterpriseId, CancellationToken cancellationToken);
}
