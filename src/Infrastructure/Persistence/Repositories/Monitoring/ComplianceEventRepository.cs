using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class ComplianceEventRepository(ApplicationDbContext context, ICurrentUserService currentUserService) :
    BaseAsyncRepository<ComplianceEvent>(context), IComplianceEventRepository, IComplianceEventQueries
{
    public async Task<IReadOnlyCollection<ComplianceEvent>> AddRangeAsync(
        IEnumerable<ComplianceEvent> entities,
        CancellationToken cancellationToken)
    {
        var events = entities as ComplianceEvent[] ?? entities.ToArray();
        await Entities.AddRangeAsync(events, cancellationToken);
        return events.ToList();
    }

    public async Task<IReadOnlyList<ComplianceEvent>> GetByMeasurementIdAsync(Guid measurementId,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        return await TableNoTracking
            .Where(x => x.MeasurementId == measurementId &&
                        (isSuperAdmin || x.EmissionSource!.Installation!.Site!.EnterpriseId ==
                            currentEnterpriseId))
            .ToListAsync(cancellationToken);
    }

    public async Task<Option<ComplianceEvent>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await Table
            .FirstOrDefaultAsync(x => x.Id == id &&
                                      (isSuperAdmin ||
                                       x.EmissionSource!.Installation!.Site!.EnterpriseId ==
                                       currentEnterpriseId), cancellationToken);

        return entity ?? Option<ComplianceEvent>.None;
    }
}
