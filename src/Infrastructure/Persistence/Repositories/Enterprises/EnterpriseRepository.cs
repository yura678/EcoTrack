using Application.Common.Interfaces.Queries.Enterprises;
using Application.Common.Interfaces.Repositories.Enterprises;
using Application.Common.Models;
using Domain.Entities.Enterprises;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Enterprises;

internal class EnterpriseRepository(ApplicationDbContext context)
    : BaseAsyncRepository<Enterprise>(context), IEnterpriseQueries, IEnterpriseRepository
{
    public async Task<IReadOnlyList<Enterprise>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveEnterpriseIdsAsync(CancellationToken cancellationToken)
    {
        // TableNoTracking honours the soft-delete query filter, so deleted enterprises are
        // automatically excluded — the scheduler only fans out work to live tenants.
        return await base.TableNoTracking
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<PageResult<Enterprise>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = base.TableNoTracking;

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<Enterprise>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Option<Enterprise>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Id.Equals(id), cancellationToken);

        return entity ?? Option<Enterprise>.None;
    }

    public async Task<Option<Enterprise>> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await base.Table
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id.Equals(id), cancellationToken);

        return entity ?? Option<Enterprise>.None;
    }

    public async Task<Option<Enterprise>> GetByIdWithSitesAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .Include(x => x.Sites)
            .FirstOrDefaultAsync(x => x.Id.Equals(id), cancellationToken);

        return entity ?? Option<Enterprise>.None;
    }

    public async Task<Option<Enterprise>> GetByEdrpouAsync(string edrpou, CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Edrpou == edrpou, cancellationToken);

        return entity ?? Option<Enterprise>.None;
    }

    public async Task<Option<Enterprise>> GetByEdrpouIncludingDeletedAsync(string edrpou, CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Edrpou == edrpou, cancellationToken);

        return entity ?? Option<Enterprise>.None;
    }

    public Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken)
    {
        var hasDependencies = context.Set<Site>().AnyAsync(x => x.EnterpriseId.Equals(id), cancellationToken);
         return hasDependencies;
    }

    public override async Task<Enterprise> AddAsync(Enterprise entity, CancellationToken cancellationToken)
    {
        return await base.AddAsync(entity, cancellationToken);
    }

    public override Enterprise Update(Enterprise entity)
    {
        return base.Update(entity);
    }

    public override Enterprise Delete(Enterprise entity)
    {
        return base.Delete(entity);
    }
}