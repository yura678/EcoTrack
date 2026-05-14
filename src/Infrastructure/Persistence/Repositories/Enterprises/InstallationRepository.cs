using Application.Common.Interfaces.Queries.Enterprises;
using Application.Common.Interfaces.Repositories.Enterprises;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Enterprises;

internal class InstallationRepository(ApplicationDbContext context)
    : BaseAsyncRepository<Installation>(context),
        IInstallationRepository, IInstallationQueries
{
    public async Task<IReadOnlyList<Installation>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Installation>> GetBySiteIdAsync(Guid siteId,
        CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .Where(x => x.SiteId == siteId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasDependenciesAsync(Guid id,
        CancellationToken cancellationToken)
    {
        return
            await DbContext.Set<Measurement>()
                .AnyAsync(x => x.EmissionSource != null &&
                               x.EmissionSource.InstallationId.Equals(id), cancellationToken)
            || await DbContext.Set<EmissionLimit>()
                .AnyAsync(x => x.EmissionSource != null &&
                               x.EmissionSource.InstallationId.Equals(id), cancellationToken)
            || await DbContext.Set<MonitoringDevice>()
                .AnyAsync(x => x.InstallationId.Equals(id), cancellationToken);
    }


    public async Task<Option<Installation>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity ?? Option<Installation>.None;
    }

    public async Task<Option<Installation>> GetByIdWithEmissionsAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .Include(x => x.EmissionSources)
            .FirstOrDefaultAsync(x => x.Id.Equals(id), cancellationToken);

        return entity ?? Option<Installation>.None;
    }
}
