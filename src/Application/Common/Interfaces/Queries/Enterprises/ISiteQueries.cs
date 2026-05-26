using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Enterprises;

public interface ISiteQueries
{
    Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Site>> GetByEnterpriseIdAsync(Guid enterpriseId, CancellationToken cancellationToken);

    /// <summary>
    /// Same as <see cref="GetByEnterpriseIdAsync"/> but bypasses the soft-delete query filter
    /// so callers can offer a "Show deleted" toggle. Used exclusively by the Sites list page.
    /// </summary>
    Task<IReadOnlyList<Site>> GetByEnterpriseIdIncludingDeletedAsync(Guid enterpriseId, CancellationToken cancellationToken);

    Task<Option<Site>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Same as <see cref="GetByIdAsync"/> but bypasses the soft-delete filter so the
    /// frontend can render details for a deleted site and offer Restore.
    /// </summary>
    Task<Option<Site>> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken);

    Task<Option<Site>> GetByIdWithInstallationsAsync(Guid id, CancellationToken cancellationToken);
}