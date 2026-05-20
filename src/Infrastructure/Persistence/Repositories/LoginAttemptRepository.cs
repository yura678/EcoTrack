using Application.Common.Interfaces.Repositories;
using Application.Common.Models;
using Domain.Entities.User;
using Infrastructure.Persistence.Repositories.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal class LoginAttemptRepository(ApplicationDbContext dbContext)
    : BaseAsyncRepository<LoginAttempt>(dbContext), ILoginAttemptRepository
{
    public async Task AddAsync(LoginAttempt entity, CancellationToken cancellationToken)
    {
        await base.Entities.AddAsync(entity, cancellationToken);
    }

    public async Task<PageResult<LoginAttempt>> GetForUserPagedAsync(
        Guid userId, DateTime? from, DateTime? to, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = base.TableNoTracking.Where(x => x.UserId == userId);
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.OccurredAt <= to.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<LoginAttempt>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
