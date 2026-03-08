using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Models;
using Domain.Entities.EmissionSources;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class MonitoringDeviceRepository(ApplicationDbContext context, ICurrentUserService currentUserService)
    : BaseAsyncRepository<MonitoringDevice>(context), IMonitoringDeviceRepository, IMonitoringDeviceQueries
{
    public async Task<IReadOnlyList<MonitoringDevice>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .ToListAsync(cancellationToken);
    }

    public async Task<PageResult<MonitoringDevice>> GetPagedAsync(
        Guid installationId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();
        var query = base.TableNoTracking
            .Where(x => x.InstallationId == installationId)
            .Where(x => isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<MonitoringDevice>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Option<MonitoringDevice>> GetByIdAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Id == id &&
                                      (isSuperAdmin || x.Installation!.Site!.EnterpriseId == currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<MonitoringDevice>.None;
    }


    public async Task<Option<MonitoringDevice>> GetByIdAsync(
        Guid emissionSourceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var device = await base.TableNoTracking
            .Where(d => d.Id == id && (isSuperAdmin || d.Installation!.Site!.EnterpriseId == currentEnterpriseId))
            .SelectMany(
                d => DbContext.Set<EmissionSource>()
                    .AsNoTracking()
                    .Where(es => es.Id == emissionSourceId),
                (d, es) => new { Device = d, Source = es }
            )
            .Where(x => x.Device.EmissionSourceId == emissionSourceId ||
                        x.Device.InstallationId == x.Source.InstallationId)
            .Select(x => x.Device)
            .FirstOrDefaultAsync(cancellationToken);

        return device ?? Option<MonitoringDevice>.None;
    }

    public async Task<Option<MonitoringDevice>> GetBySerialNumberAsync(string serialNumber,
        CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber, cancellationToken);

        return entity ?? Option<MonitoringDevice>.None;
    }

    public Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken)
    {
        return DbContext.Set<Measurement>().AnyAsync(x => x.DeviceId.Equals(id), cancellationToken);
    }
}