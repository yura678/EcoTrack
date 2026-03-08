using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Emissions;
using Application.Common.Interfaces.Repositories.Emissions;
using Application.Common.Models;
using Application.Models.EmissionSources;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.EmissionSources;

internal class EmissionSourceRepository(ApplicationDbContext context,
    ICurrentUserService currentUserService)
    : BaseAsyncRepository<EmissionSource>(context),
        IEmissionSourceQueries, IEmissionSourceRepository
{
    public async Task<IReadOnlyList<EmissionSource>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .ToListAsync(cancellationToken);
    }

    public async Task<PageResult<EmissionSource>> GetPagedAsync(
        Guid installationId,
        EmissionSourceType? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var query = base.TableNoTracking
            .Where(x => x.InstallationId == installationId)
            .Where(x => isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId);
        
        if (type == EmissionSourceType.Air)
        {
            query = query.OfType<AirEmissionSource>();
        }
        else if (type == EmissionSourceType.Water)
        {
            query = query.OfType<WaterEmissionSource>();
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<EmissionSource>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }


    public async Task<Option<EmissionSource>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Id == id && 
                                      (isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId), 
                cancellationToken);

        return entity ?? Option<EmissionSource>.None;
    }

    public async Task<IReadOnlyList<EmissionSource>> GetByEnterpriseAndIdsAsync(
        Guid enterpriseId,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .Where(x => ids.Contains(x.Id))
            .Where(x => x.Installation.Site.EnterpriseId.Equals(enterpriseId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmissionSource>> GetByInstallationAndIdsAsync(Guid installationId,
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .Where(x => ids.Contains(x.Id))
            .Where(x => x.InstallationId.Equals(installationId))
            .ToListAsync(cancellationToken);
    }


    public async Task<Option<EmissionSource>> GetByCodeAsync(Guid installationId, string code, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Code == code && 
                                      x.InstallationId == installationId &&
                                      (isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId), 
                cancellationToken);

        return entity ?? Option<EmissionSource>.None;
    }

    public async Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken)
    {
        var hasDependencies =
            await DbContext.Set<Measurement>().AnyAsync(x => x.EmissionSourceId.Equals(id), cancellationToken) ||
            await DbContext.Set<EmissionLimit>().AnyAsync(x => x.EmissionSourceId.Equals(id), cancellationToken) ||
            await DbContext.Set<MonitoringRequirement>()
                .AnyAsync(x => x.EmissionSourceId.Equals(id), cancellationToken);

        return hasDependencies;
    }

    public async Task<IReadOnlyList<EmissionSource>> GetByInstallationIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        return await base.TableNoTracking
            .Where(x => x.InstallationId == id &&
                        (isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId))
            .ToListAsync(cancellationToken);
    }
}