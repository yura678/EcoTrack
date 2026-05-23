using Application.Common.Models;
using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Monitoring;

public interface IComplianceEventQueries
{
    Task<IReadOnlyList<ComplianceEvent>> GetByMeasurementIdAsync(Guid measurementId,
        CancellationToken cancellationToken);

    /// <param name="enterpriseId">When non-null, scope the result to a single tenant. The per-tenant
    /// Hangfire detection fan-out passes its own enterprise so it doesn't fetch every other
    /// tenant's open events on each 5-min tick.</param>
    Task<IReadOnlyList<ComplianceEvent>> GetOpenByTypeAsync(ComplianceEventType eventType,
        CancellationToken cancellationToken, Guid? enterpriseId = null);

    Task<Option<ComplianceEvent>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PageResult<ComplianceEvent>> GetPagedAsync(
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
        CancellationToken cancellationToken);
}
