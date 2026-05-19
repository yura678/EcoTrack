using Application.Common.Models;
using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Monitoring;

public interface IComplianceEventQueries
{
    Task<IReadOnlyList<ComplianceEvent>> GetByMeasurementIdAsync(Guid measurementId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ComplianceEvent>> GetOpenByTypeAsync(ComplianceEventType eventType,
        CancellationToken cancellationToken);

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
