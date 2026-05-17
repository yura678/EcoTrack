using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Monitoring;

public interface IMeasurementRepository
{
    Task<Option<Measurement>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Option<Measurement>> GetByTimeStamp(DateTime timestamp, Guid pollutantId,
        Guid emissionSourceId, CancellationToken cancellationToken);

    Task<Measurement> AddAsync(Measurement entity, CancellationToken cancellationToken);
    Task AddRangeAsync(IEnumerable<Measurement> entities, CancellationToken cancellationToken);
    Measurement Update(Measurement entity);

    /// <summary>
    /// Loads tracked Measurement entities (Average aggregation) whose WindowStart falls inside
    /// the given range for any of the (sourceId, pollutantId) pairs. Used by the materializer's
    /// late-arriving-data re-scan to mutate existing records in place.
    /// </summary>
    Task<IReadOnlyList<Measurement>> GetForRescanAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        AveragingWindow window,
        DateTime fromWindowStart,
        DateTime toWindowStart,
        CancellationToken cancellationToken);
}