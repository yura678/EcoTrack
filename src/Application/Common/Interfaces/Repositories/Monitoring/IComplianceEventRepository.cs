using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Monitoring;

public interface IComplianceEventRepository
{
    Task<IReadOnlyCollection<ComplianceEvent>> AddRangeAsync(IEnumerable<ComplianceEvent> entities,
        CancellationToken cancellationToken);

    Task<ComplianceEvent> AddAsync(ComplianceEvent entity, CancellationToken cancellationToken);
    ComplianceEvent Update(ComplianceEvent entity);

    Task<IReadOnlyList<ComplianceEvent>> GetByMeasurementIdAsync(Guid measurementId,
        CancellationToken cancellationToken);

    Task<Option<ComplianceEvent>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
