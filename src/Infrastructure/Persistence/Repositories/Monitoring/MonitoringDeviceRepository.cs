using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Models;
using Domain.Entities.EmissionSources;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class MonitoringDeviceRepository(ApplicationDbContext context)
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
        var query = base.TableNoTracking
            .Include(x => x.Installation)
            .Include(x => x.EmissionSource)
            .Where(x => x.InstallationId == installationId);

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
        var entity = await base.TableNoTracking
            .Include(x => x.Installation)
            .Include(x => x.EmissionSource)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity ?? Option<MonitoringDevice>.None;
    }


    public async Task<Option<MonitoringDevice>> GetByIdAsync(
        Guid emissionSourceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var device = await base.TableNoTracking
            .Where(d => d.Id == id)
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
        // Used by HMAC ingest auth (no user context: tenancy bypass) and as a "find live device"
        // helper. Soft-delete filter still applies — decommissioned devices are hidden.
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber, cancellationToken);

        return entity ?? Option<MonitoringDevice>.None;
    }

    public async Task<Option<MonitoringDevice>> GetBySerialNumberIncludingDeletedAsync(string serialNumber,
        CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber, cancellationToken);

        return entity ?? Option<MonitoringDevice>.None;
    }

    public Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken)
    {
        // Source-of-truth for "what this device has produced" is raw_measurement, not
        // Measurement: materialised aggregates leave DeviceId null because a window can have
        // contributions from multiple devices, so checking Measurement.DeviceId would both
        // miss real dependencies AND falsely block deletion of devices that just happened to
        // be picked as the (now removed) "first device" representative.
        return DbContext.Set<RawMeasurement>().AnyAsync(x => x.DeviceId == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MonitoringDevice>> GetByInstallationIdsAsync(
        IReadOnlyList<Guid> installationIds, CancellationToken cancellationToken)
    {
        return await base.Table
            .Where(x => installationIds.Contains(x.InstallationId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MonitoringDevice>> GetByEmissionSourceIdAsync(
        Guid emissionSourceId, CancellationToken cancellationToken)
    {
        return await base.Table
            .Where(x => x.EmissionSourceId == emissionSourceId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MonitoringDevice>> GetNonDecommissionedByInstallationAsync(
        Guid installationId, CancellationToken cancellationToken)
    {
        return await base.Table
            .Where(x => x.InstallationId == installationId
                        && x.Status != DeviceStatus.Decommissioned)
            .ToListAsync(cancellationToken);
    }
}
