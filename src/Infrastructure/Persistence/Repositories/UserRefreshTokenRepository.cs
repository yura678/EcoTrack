using Application.Common.Interfaces.Persistence;
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
}
