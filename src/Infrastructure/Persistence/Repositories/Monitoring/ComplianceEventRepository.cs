using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Models;
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

    public async Task<IReadOnlyList<ComplianceEvent>> GetOpenByTypeAsync(ComplianceEventType eventType,
        CancellationToken cancellationToken, Guid? enterpriseId = null)
    {
        var query = TableNoTracking
            .Where(x => x.EventType == eventType && x.Status == ComplianceEventStatus.Open);
        if (enterpriseId.HasValue)
        {
            query = query.Where(x => x.EnterpriseId == enterpriseId.Value);
        }
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PageResult<ComplianceEvent>> GetPagedAsync(
        ComplianceEventStatus? status,
        ComplianceEventType? eventType,
        Guid? emissionSourceId,
        Guid? deviceId,
        Guid? installationId,
        Guid? siteId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = TableNoTracking.AsQueryable();

        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (eventType.HasValue) query = query.Where(x => x.EventType == eventType.Value);
        if (emissionSourceId.HasValue) query = query.Where(x => x.EmissionSourceId == emissionSourceId.Value);
        if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId.Value);
        if (from.HasValue) query = query.Where(x => x.WindowEnd >= from.Value);
        if (to.HasValue) query = query.Where(x => x.WindowEnd <= to.Value);

        // Installation/site scoping is resolved via subquery on EmissionSource — keeps the
        // ComplianceEvent table free of denormalised installation/site columns and stays
        // tenant-filter-aware (the global query filter applies to both tables).
        if (installationId.HasValue)
        {
            var installationSourceIds = context.Set<Domain.Entities.EmissionSources.EmissionSource>()
                .Where(s => s.InstallationId == installationId.Value)
                .Select(s => s.Id);
            query = query.Where(x => installationSourceIds.Contains(x.EmissionSourceId));
        }
        if (siteId.HasValue)
        {
            var siteSourceIds = context.Set<Domain.Entities.EmissionSources.EmissionSource>()
                .Where(s => s.Installation!.SiteId == siteId.Value)
                .Select(s => s.Id);
            query = query.Where(x => siteSourceIds.Contains(x.EmissionSourceId));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.DetectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<ComplianceEvent>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
