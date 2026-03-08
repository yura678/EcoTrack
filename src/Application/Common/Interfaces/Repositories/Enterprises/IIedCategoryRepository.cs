using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Enterprises;

public interface IIedCategoryRepository
{
    Task<Option<IedCategory>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}