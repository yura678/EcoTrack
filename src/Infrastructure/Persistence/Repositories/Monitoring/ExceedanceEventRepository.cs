using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class ExceedanceEventRepository(ApplicationDbContext context, ICurrentUserService currentUserService) :
    BaseAsyncRepository<ExceedanceEvent>(context), IExceedanceEventRepository, IExceedanceEventQueries
{
    public async Task<IReadOnlyCollection<ExceedanceEvent>> AddRangeAsync(
        IEnumerable<ExceedanceEvent> entities,
        CancellationToken cancellationToken)
    {
        var exceedanceEvents = entities as ExceedanceEvent[] ?? entities.ToArray();
        await Entities.AddRangeAsync(exceedanceEvents, cancellationToken);
        return exceedanceEvents.ToList();
    }


    public async Task<IReadOnlyList<ExceedanceEvent>> GetByMeasurementIdAsync(Guid measurementId,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        return await base.TableNoTracking
            .Where(x => x.MeasurementId == measurementId &&
                        (isSuperAdmin || x.Measurement!.EmissionSource!.Installation!.Site!.EnterpriseId ==
                            currentEnterpriseId))
            .ToListAsync(cancellationToken);
    }

    public async Task<Option<ExceedanceEvent>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.Table
            .FirstOrDefaultAsync(x => x.Id == id &&
                        (isSuperAdmin || x.Measurement!.EmissionSource!.Installation!.Site!.EnterpriseId ==
                            currentEnterpriseId), cancellationToken);

        return entity;
    }
}