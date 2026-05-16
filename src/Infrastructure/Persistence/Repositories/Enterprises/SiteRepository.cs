using Application.Common.Interfaces.Queries.Enterprises;
using Application.Common.Interfaces.Repositories.Enterprises;
using Domain.Entities.Enterprises;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Enterprises;

internal class SiteRepository(ApplicationDbContext context)
    : BaseAsyncRepository<Site>(context),
        ISiteRepository, ISiteQueries
{
    public async Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Site>> GetByEnterpriseIdAsync(Guid enterpriseId,
        CancellationToken cancellationToken)
    {
        return await base.Table
            .Where(x => x.EnterpriseId == enterpriseId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Option<Site>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity ?? Option<Site>.None;
    }

    public async Task<Option<Site>> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await base.Table
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity ?? Option<Site>.None;
    }

    public async Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken)
    {
        var hasDependencies = await DbContext.Set<Installation>().AnyAsync(x => x.SiteId.Equals(id), cancellationToken);

        return hasDependencies;
    }

    public async Task<Option<Site>> GetByIdWithInstallationsAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .Include(x => x.Installations)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity ?? Option<Site>.None;
    }
}
