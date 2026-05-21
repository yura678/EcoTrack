using Domain.Entities.EmissionSources;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Emissions;

public interface IPollutantQueries
{
    Task<IReadOnlyList<Pollutant>> GetAllAsync(CancellationToken cancellationToken);
    Task<Option<Pollutant>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Option<Pollutant>> GetByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Batch load — used by the ingest handler to read each pollutant's CanonicalUnitId +
    /// MolarMass once per request, instead of N round-trips for an N-row batch.
    /// </summary>
    Task<IReadOnlyList<Pollutant>> GetByIdsAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}