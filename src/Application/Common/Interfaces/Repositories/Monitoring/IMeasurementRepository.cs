using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Monitoring;

public interface IMeasurementRepository
{
    Task<Option<Measurement>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Option<Measurement>> GetByTimeStamp(DateTime timestamp, Guid pollutantId,
        Guid emissionSourceId, CancellationToken cancellationToken);

    Task<Measurement> AddAsync(Measurement entity, CancellationToken cancellationToken);
    Measurement Update(Measurement entity);
}