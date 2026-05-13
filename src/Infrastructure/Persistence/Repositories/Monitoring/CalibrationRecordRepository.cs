using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class CalibrationRecordRepository(
    ApplicationDbContext context,
    ICurrentUserService currentUserService)
    : BaseAsyncRepository<CalibrationRecord>(context),
        ICalibrationRecordRepository, ICalibrationRecordQueries
{
    public async Task<Option<CalibrationRecord>> GetByIdAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await Table
            .FirstOrDefaultAsync(
                x => x.Id == id &&
                     (isSuperAdmin ||
                      x.Device!.Installation!.Site!.EnterpriseId == currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<CalibrationRecord>.None;
    }

    public async Task<IReadOnlyList<CalibrationRecord>> GetByDeviceIdAsync(
        Guid deviceId, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        return await TableNoTracking
            .Where(x => x.DeviceId == deviceId &&
                        (isSuperAdmin ||
                         x.Device!.Installation!.Site!.EnterpriseId == currentEnterpriseId))
            .OrderByDescending(x => x.PerformedAt)
            .ToListAsync(cancellationToken);
    }
}
