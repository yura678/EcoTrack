using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Enterprises;

public interface IIedCategoryQueries
{
    Task<IReadOnlyList<IedCategory>> GetAllAsync(CancellationToken cancellationToken);
    Task<Option<IedCategory>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}