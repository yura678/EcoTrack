using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Enterprises;

public interface IInstallationQueries
{
    Task<IReadOnlyList<Installation>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Installation>> GetBySiteIdAsync(Guid siteId, CancellationToken cancellationToken);
    Task<Option<Installation>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Option<Installation>> GetByIdWithEmissionsAsync(Guid id, CancellationToken cancellationToken);
}