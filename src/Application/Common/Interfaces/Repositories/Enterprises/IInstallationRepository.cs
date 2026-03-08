using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Enterprises;

public interface IInstallationRepository
{
    Task<Installation> AddAsync(Installation entity, CancellationToken cancellationToken);
    Installation Update(Installation entity);
    Installation Delete(Installation entity);
    Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken);

    Task<Option<Installation>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

}