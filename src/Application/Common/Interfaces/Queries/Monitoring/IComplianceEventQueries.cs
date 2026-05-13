using Domain.Entities.Monitoring;

namespace Application.Common.Interfaces.Queries.Monitoring;

public interface IComplianceEventQueries
{
    Task<IReadOnlyList<ComplianceEvent>> GetByMeasurementIdAsync(Guid measurementId,
        CancellationToken cancellationToken);
}
