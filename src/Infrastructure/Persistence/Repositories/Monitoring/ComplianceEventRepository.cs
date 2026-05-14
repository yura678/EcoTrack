using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class ComplianceEventRepository(ApplicationDbContext context) :
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
        return await TableNoTracking
            .Where(x => x.MeasurementId == measurementId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Option<ComplianceEvent>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await Table
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity ?? Option<ComplianceEvent>.None;
    }
}
