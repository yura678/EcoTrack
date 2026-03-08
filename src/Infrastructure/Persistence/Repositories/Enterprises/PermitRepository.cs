using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Enterprises;
using Application.Common.Interfaces.Repositories.Enterprises;
using Application.Common.Models;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Enterprises;

internal class PermitRepository(ApplicationDbContext context, ICurrentUserService currentUserService)
    : BaseAsyncRepository<Permit>(context), IPermitRepository, IPermitQueries
{
    public async Task<Option<Permit>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .Include(x => x.EmissionLimits)
            .FirstOrDefaultAsync(x => x.Id == id &&
                                      (isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<Permit>.None;
    }


    public async Task<Option<Permit>> GetByNumberAsync(string number, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Number == number &&
                                      (isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<Permit>.None;
    }

    public async Task<PageResult<Permit>> GetPagedAsync(Guid installationId,
        DateTime? from, DateTime? to, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var query = base.TableNoTracking
            .Include(x => x.EmissionLimits)
            .Where(x => x.InstallationId == installationId)
            .Where(x => isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId);

        query = query.Where(x => x.InstallationId.Equals(installationId));

        if (from.HasValue)
        {
            query = query.Where(x => x.IssuedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.IssuedAt <= to.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<Permit>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<bool> HasDependenciesAsync(Guid permitId, CancellationToken cancellationToken)
    {
        return await DbContext.Set<ExceedanceEvent>()
            .AsNoTracking()
            .AnyAsync(x => x.Limit.PermitId.Equals(permitId), cancellationToken);
    }

    public async Task<Option<Permit>> GetActiveByEmissionSourceAsync(
        Guid sourceId,
        DateTime activeAt,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var permit = await base.Table
            .Include(p => p.EmissionLimits!)
            .ThenInclude(l => l.Unit)
            .FirstOrDefaultAsync(p =>
                    p.IssuedAt <= activeAt &&
                    p.ValidUntil >= activeAt &&
                    p.EmissionLimits!.Any(l => l.EmissionSourceId == sourceId) &&
                    (isSuperAdmin || p.Installation!.Site!.EnterpriseId == currentEnterpriseId),
                cancellationToken);

        return permit ?? Option<Permit>.None;
    }


    public async Task<Option<Permit>> GetActiveAsync(
        Guid installationId,
        PermitType permitType,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.PermitStatus == PermitStatus.Active
                                      && x.PermitType == permitType
                                      && x.InstallationId == installationId
                                      && (isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<Permit>.None;
    }
}