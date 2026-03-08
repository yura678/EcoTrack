using Domain.Entities.EmissionSources;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Emissions;

public interface IAirEmissionQueries
{
    Task<IReadOnlyList<AirEmissionSource>> GetAllAsync(CancellationToken cancellationToken);
    Task<Option<AirEmissionSource>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}