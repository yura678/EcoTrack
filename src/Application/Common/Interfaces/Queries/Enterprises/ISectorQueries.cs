using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Enterprises;

public interface ISectorQueries
{
    Task<IReadOnlyList<Sector>> GetAllAsync(CancellationToken cancellationToken);
    Task<Option<Sector>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}