using Domain.Entities.EmissionSources;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Emissions;

public interface IPollutantQueries
{
    Task<IReadOnlyList<Pollutant>> GetAllAsync(CancellationToken cancellationToken);
    Task<Option<Pollutant>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Option<Pollutant>> GetByCodeAsync(string code, CancellationToken cancellationToken);
}