using Application.Common.Interfaces.Queries.Enterprises;
using Application.Common.Interfaces.Repositories.Enterprises;
using Application.Common.Models;
using Domain.Entities.Enterprises;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Enterprises;

internal class PermitRepository(ApplicationDbContext context)
    : BaseAsyncRepository<Permit>(context), IPermitRepository, IPermitQueries
{
    public async Task<Option<Permit>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        // Tracked load — every command handler (Update, Activate, Revoke, Archive, Delete)
        // mutates this entity and SaveChanges. With AsNoTracking, re-attaching via Update()
        // triggers EF relationship fixup that can resurrect a stale EmissionLimit FK from
        // its navigation property (e.g. EmissionSourceId is cleared but EmissionSource was
        // loaded, so EF reverts the FK). Tracking the graph from the start avoids that.
        var entity = await base.Table
            .Include(x => x.Installation)
            .Include(x => x.EmissionLimits!).ThenInclude(l => l.Unit)
            .Include(x => x.EmissionLimits!).ThenInclude(l => l.Pollutant)
            .Include(x => x.EmissionLimits!).ThenInclude(l => l.EmissionSource)
            .Include(x => x.EmissionLimits!).ThenInclude(l => l.Installation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity ?? Option<Permit>.None;
    }


    public async Task<Option<Permit>> GetByNumberAsync(string number, CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Number == number, cancellationToken);

        return entity ?? Option<Permit>.None;
    }

    public async Task<Option<Permit>> GetByNumberIncludingDeletedAsync(string number,
        CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Number == number, cancellationToken);

        return entity ?? Option<Permit>.None;
    }

    public async Task<PageResult<Permit>> GetPagedAsync(Guid installationId,
        DateTime? from, DateTime? to, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = base.TableNoTracking
            .Include(x => x.EmissionLimits!).ThenInclude(l => l.Unit)
            .Include(x => x.EmissionLimits!).ThenInclude(l => l.Pollutant)
            .Include(x => x.EmissionLimits!).ThenInclude(l => l.EmissionSource)
            .Where(x => x.InstallationId == installationId);

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

    public async Task<Option<Permit>> GetActiveByEmissionSourceAsync(
        Guid sourceId,
        DateTime activeAt,
        CancellationToken cancellationToken)
    {
        var permit = await base.Table
            .Include(p => p.EmissionLimits!)
            .ThenInclude(l => l.Unit)
            .FirstOrDefaultAsync(p =>
                    p.IssuedAt <= activeAt &&
                    p.ValidUntil >= activeAt &&
                    p.EmissionLimits!.Any(l => l.EmissionSourceId == sourceId),
                cancellationToken);

        return permit ?? Option<Permit>.None;
    }


    public async Task<Option<Permit>> GetActiveAsync(
        Guid installationId,
        PermitType permitType,
        CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.PermitStatus == PermitStatus.Active
                                      && x.PermitType == permitType
                                      && x.InstallationId == installationId,
                cancellationToken);

        return entity ?? Option<Permit>.None;
    }

    public async Task<IReadOnlyList<Permit>> GetActiveByInstallationAsync(
        Guid installationId, CancellationToken cancellationToken)
    {
        return await base.Table
            .Where(x => x.InstallationId == installationId
                        && x.PermitStatus == PermitStatus.Active)
            .ToListAsync(cancellationToken);
    }
}
