using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Models;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class MonitoringPlanRepository(ApplicationDbContext context, ICurrentUserService currentUserService) :
    BaseAsyncRepository<MonitoringPlan>(context), IMonitoringPlanRepository, IMonitoringPlanQueries
{
    public async Task<Option<MonitoringPlan>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .Include(x => x.Requirements)
            .FirstOrDefaultAsync(x => x.Id == id &&
                                      (isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<MonitoringPlan>.None;
    }

    public async Task<Option<MonitoringPlan>> GetActiveByInstallationAsync(
        Guid installationId,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .Include(x => x.Requirements)
            .FirstOrDefaultAsync(x => x.Status == MonitoringPlanStatus.Active
                                      && x.InstallationId == installationId
                                      && (isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<MonitoringPlan>.None;
    }

    public async Task<Option<MonitoringPlan>> GetActiveByEmissionSourceAsync(
        Guid emissionSourceId,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .Include(x => x.Requirements)
            .FirstOrDefaultAsync(x =>
                    x.Status == MonitoringPlanStatus.Active &&
                    x.Installation!.EmissionSources!.Any(es => es.Id == emissionSourceId) &&
                    (isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<MonitoringPlan>.None;
    }


    public async Task<PageResult<MonitoringPlan>> GetPagedAsync(
        Guid installationId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();
        var query = base.TableNoTracking
            .Include(x => x.Requirements)
            .Where(x => x.InstallationId == installationId)
            .Where(x => isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId);

        query = query.Where(x => x.InstallationId.Equals(installationId));

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<MonitoringPlan>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}