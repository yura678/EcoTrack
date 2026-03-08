using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Enterprises;

public interface ISectorRepository
{
    Task<Option<Sector>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}