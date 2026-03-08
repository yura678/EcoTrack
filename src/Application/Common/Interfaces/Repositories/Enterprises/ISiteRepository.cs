using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Enterprises;

public interface ISiteRepository
{
    Task<Site> AddAsync(Site entity, CancellationToken cancellationToken);
    Site Update(Site entity);
    Site Delete(Site entity);
    Task<Option<Site>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken);
}