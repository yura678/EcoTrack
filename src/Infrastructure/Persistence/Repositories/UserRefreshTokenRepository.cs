using Application.Common.Interfaces.Persistence;
using Application.Models.Profile;
using Domain.Entities.Enterprises;
using Domain.Entities.User;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal class UserRefreshTokenRepository(ApplicationDbContext dbContext)
    : BaseAsyncRepository<UserRefreshToken>(dbContext), IUserRefreshTokenRepository
{
    public async Task<Guid> CreateToken(Guid userId, Guid? enterpriseId, DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        var token = new UserRefreshToken
        {
            IsValid = true,
            UserId = userId,
            EnterpriseId = enterpriseId,
            ExpiresAt = expiresAt
        };
        await base.AddAsync(token, cancellationToken);
        return token.Id;
    }

    public async Task<Option<UserRefreshToken>> GetTokenWithInvalidation(Guid id)
    {
        var now = DateTime.UtcNow;
        var token = await base.Table
            .Where(t => t.IsValid && t.ExpiresAt > now && t.Id.Equals(id))
            .FirstOrDefaultAsync();

        return token;
    }

    public async Task<Option<User>> GetUserByRefreshToken(Guid tokenId)
    {
        var user = await base.TableNoTracking.Include(t => t.User).Where(c => c.Id.Equals(tokenId))
            .Select(c => c.User).FirstOrDefaultAsync();

        return user;
    }

    public async Task InvalidateAllForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await base.Table
            .Where(t => t.UserId == userId && t.IsValid)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsValid, false), cancellationToken);
    }

    public async Task InvalidateAllForUserAndEnterpriseAsync(
        Guid userId, Guid enterpriseId, CancellationToken cancellationToken)
    {
        await base.Table
            .Where(t => t.UserId == userId
                        && t.EnterpriseId == enterpriseId
                        && t.IsValid)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsValid, false), cancellationToken);
    }

    public async Task<IReadOnlyList<SessionInfo>> GetActiveSessionsForUserAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        // Manual join to Enterprise so a null EnterpriseId yields null EnterpriseName instead
        // of dropping the row (LINQ-to-EF translates the equality below to LEFT JOIN).
        var rows = await (
            from t in base.TableNoTracking
            where t.UserId == userId && t.IsValid && t.ExpiresAt > now
            join e in dbContext.Set<Enterprise>().IgnoreQueryFilters()
                on t.EnterpriseId equals e.Id into joined
            from e in joined.DefaultIfEmpty()
            orderby t.CreatedAt descending
            select new SessionInfo(
                t.Id, t.EnterpriseId, e == null ? null : e.Name, t.CreatedAt, t.ExpiresAt))
            .ToListAsync(cancellationToken);
        return rows;
    }

    public async Task<bool> InvalidateForUserAsync(
        Guid tokenId, Guid userId, CancellationToken cancellationToken)
    {
        // SetProperty + composite WHERE so an attacker who guesses a token id of another user
        // still gets a zero-row update. ExecuteUpdate returns affected row count.
        var affected = await base.Table
            .Where(t => t.Id == tokenId && t.UserId == userId && t.IsValid)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsValid, false), cancellationToken);
        return affected > 0;
    }
}
