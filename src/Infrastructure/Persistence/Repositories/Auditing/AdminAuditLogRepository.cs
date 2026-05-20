using Application.Common.Interfaces.Repositories.Auditing;
using Application.Common.Models;
using Domain.Entities.Auditing;
using Infrastructure.Persistence.Repositories.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Auditing;

internal class AdminAuditLogRepository(ApplicationDbContext context)
    : BaseAsyncRepository<AdminAuditLog>(context), IAdminAuditLogRepository
{
    public async Task AddAsync(AdminAuditLog entity, CancellationToken cancellationToken)
    {
        await Entities.AddAsync(entity, cancellationToken);
    }

    public async Task<PageResult<AdminAuditLog>> GetPagedAsync(
        AuditAction? action,
        AuditTargetType? targetType,
        Guid? targetId,
        Guid? actorUserId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = TableNoTracking.AsQueryable();

        if (action.HasValue) query = query.Where(x => x.Action == action.Value);
        if (targetType.HasValue) query = query.Where(x => x.TargetType == targetType.Value);
        if (targetId.HasValue) query = query.Where(x => x.TargetId == targetId.Value);
        if (actorUserId.HasValue) query = query.Where(x => x.ActorUserId == actorUserId.Value);
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.OccurredAt <= to.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<AdminAuditLog>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
