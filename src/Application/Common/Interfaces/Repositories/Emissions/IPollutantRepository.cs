using Domain.Entities.EmissionSources;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Emissions;

public interface IPollutantRepository
{
    Task<Pollutant> AddAsync(Pollutant entity, CancellationToken cancellationToken);
    Pollutant Update(Pollutant entity);
    Pollutant Delete(Pollutant entity);
    Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken);
    Task<Option<Pollutant>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Pollutant>> GetByIdsAsync(IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken);

    Task<Option<Pollutant>> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<Option<Pollutant>> GetByNameAsync(string name, CancellationToken cancellationToken);
}