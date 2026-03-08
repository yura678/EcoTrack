using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Enterprises;
using Application.Common.Interfaces.Repositories.Enterprises;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Enterprises;

internal class InstallationRepository(ApplicationDbContext context, ICurrentUserService currentUserService)
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
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        return await base.TableNoTracking
            .Include(x => x.Site)
            .Where(x => x.SiteId == siteId &&
                        (isSuperAdmin || x.Site!.EnterpriseId == currentEnterpriseId))
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
                .AnyAsync(x => x.InstallationId.Equals(id), cancellationToken)
            || await DbContext.Set<MonitoringRequirement>()
                .AnyAsync(x => x.EmissionSource != null &&
                               x.EmissionSource.InstallationId.Equals(id), cancellationToken);
    }


    public async Task<Option<Installation>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .Include(x => x.Site)
            .FirstOrDefaultAsync(x => x.Id == id &&
                                      (isSuperAdmin || x.Site!.EnterpriseId == currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<Installation>.None;
    }

    public async Task<Option<Installation>> GetByIdWithEmissionsAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .Include(x => x.EmissionSources)
            .FirstOrDefaultAsync(x => x.Id.Equals(id) &&
                                      (isSuperAdmin || x.Site!.EnterpriseId == currentEnterpriseId), cancellationToken);

        return entity ?? Option<Installation>.None;
    }
}