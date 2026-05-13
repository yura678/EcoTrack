using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class DevicePollutantCapabilityRepository(
    ApplicationDbContext context,
    ICurrentUserService currentUserService)
    : BaseAsyncRepository<DevicePollutantCapability>(context),
        IDevicePollutantCapabilityRepository,
        IDevicePollutantCapabilityQueries
{
    public async Task<Option<DevicePollutantCapability>> GetByIdAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await Table
            .FirstOrDefaultAsync(
                x => x.Id == id &&
                     (isSuperAdmin || x.Device!.Installation!.Site!.EnterpriseId == currentEnterpriseId),
                cancellationToken);

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
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        return await TableNoTracking
            .Where(x => x.DeviceId == deviceId &&
                        (isSuperAdmin ||
                         x.Device!.Installation!.Site!.EnterpriseId == currentEnterpriseId))
            .ToListAsync(cancellationToken);
    }
}
