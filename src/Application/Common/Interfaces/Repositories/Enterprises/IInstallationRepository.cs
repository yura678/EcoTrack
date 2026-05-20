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

    Task<IReadOnlyList<Installation>> GetBySiteIdsAsync(IReadOnlyList<Guid> siteIds, CancellationToken cancellationToken);

    /// <summary>
    /// Counts installations of the site that are still in <see cref="InstallationStatus.Operating"/>.
    /// Used as the pre-delete validation for Site soft-delete. No-tracking so the calling
    /// handler can safely modify the Site entity in the same context without EF severing the
    /// relationship between the to-be-deleted Site and its tracked installations.
    /// </summary>
    Task<int> CountOperatingBySiteAsync(Guid siteId, CancellationToken cancellationToken);
}