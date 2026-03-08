using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Monitoring;

public interface IExceedanceEventRepository
{
    Task<IReadOnlyCollection<ExceedanceEvent>> AddRangeAsync(IEnumerable<ExceedanceEvent> entities,
        CancellationToken cancellationToken);

    Task<ExceedanceEvent> AddAsync(ExceedanceEvent entity, CancellationToken cancellationToken);
    ExceedanceEvent Update(ExceedanceEvent entity);

    Task<IReadOnlyList<ExceedanceEvent>> GetByMeasurementIdAsync(Guid measurementId,
        CancellationToken cancellationToken);
}