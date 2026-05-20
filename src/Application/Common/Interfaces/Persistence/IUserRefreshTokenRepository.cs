using Application.Models.Profile;
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

    /// <summary>
    /// Returns active (IsValid=true, ExpiresAt &gt; now) sessions for the user with the
    /// enterprise name joined in for UI display. Ordered newest-first.
    /// </summary>
    Task<IReadOnlyList<SessionInfo>> GetActiveSessionsForUserAsync(
        Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Flips IsValid=false on a single token, scoped to the owning user. The userId predicate
    /// is a security guard — without it a leaked token id could be revoked by an attacker for
    /// any user. Returns true when a row was flipped, false when no live token matched.
    /// </summary>
    Task<bool> InvalidateForUserAsync(
        Guid tokenId, Guid userId, CancellationToken cancellationToken);
}
