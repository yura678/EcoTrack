using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class DevicePollutantCapabilityRepository(ApplicationDbContext context)
    : BaseAsyncRepository<DevicePollutantCapability>(context),
        IDevicePollutantCapabilityRepository,
        IDevicePollutantCapabilityQueries
{
    public async Task<Option<DevicePollutantCapability>> GetByIdAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await Table
            .Include(x => x.Pollutant)
            .Include(x => x.RangeUnit)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity ?? Option<DevicePollutantCapability>.None;
    }

    public async Task<Option<DevicePollutantCapability>> GetByDeviceAndPollutantAsync(
        Guid deviceId, Guid pollutantId, CancellationToken cancellationToken)
    {
        var entity = await TableNoTracking
            .FirstOrDefaultAsync(
                x => x.DeviceId == deviceId && x.PollutantId == pollutantId,
                cancellationToken);

        return entity ?? Option<DevicePollutantCapability>.None;
    }

    public async Task<IReadOnlyList<DevicePollutantCapability>> GetByDeviceIdAsync(
        Guid deviceId, CancellationToken cancellationToken)
    {
        return await TableNoTracking
            .Include(x => x.Pollutant)
            .Include(x => x.RangeUnit)
            .Where(x => x.DeviceId == deviceId)
            .ToListAsync(cancellationToken);
    }

    public async Task<System.Collections.Generic.HashSet<Guid>> GetConfiguredPollutantsForDeviceAsync(
        Guid deviceId,
        IReadOnlyCollection<Guid> pollutantIds,
        CancellationToken cancellationToken)
    {
        if (pollutantIds.Count == 0) return [];
        var rows = await TableNoTracking
            .Where(x => x.DeviceId == deviceId && pollutantIds.Contains(x.PollutantId))
            .Select(x => x.PollutantId)
            .ToListAsync(cancellationToken);
        return rows.ToHashSet();
    }

    public async Task<IReadOnlyDictionary<Guid, DeviceCapabilityRange>> GetCapabilityRangesForDeviceAsync(
        Guid deviceId,
        IReadOnlyCollection<Guid> pollutantIds,
        CancellationToken cancellationToken)
    {
        if (pollutantIds.Count == 0) return new Dictionary<Guid, DeviceCapabilityRange>();
        var rows = await TableNoTracking
            .Where(x => x.DeviceId == deviceId && pollutantIds.Contains(x.PollutantId))
            .Select(x => new DeviceCapabilityRange(
                x.PollutantId, x.RangeMin, x.RangeMax, x.RangeUnitId))
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.PollutantId);
    }
}
