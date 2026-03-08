using Domain.Entities.Monitoring;

namespace Application.Common.Interfaces.Queries.Monitoring;

public interface IExceedanceEventQueries
{
    Task<IReadOnlyList<ExceedanceEvent>> GetByMeasurementIdAsync(Guid measurementId,
        CancellationToken cancellationToken);
}