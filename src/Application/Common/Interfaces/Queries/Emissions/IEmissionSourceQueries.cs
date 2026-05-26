using Application.Common.Models;
using Application.Models.EmissionSources;
using Domain.Entities.EmissionSources;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Emissions;

public interface IEmissionSourceQueries
{
    public Task<IReadOnlyList<EmissionSource>> GetAllAsync(CancellationToken cancellationToken);

    public Task<PageResult<EmissionSource>> GetPagedAsync(Guid installationId,
        EmissionSourceType? type,
        int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Same as <see cref="GetPagedAsync"/> but bypasses the soft-delete query filter so the
    /// frontend can offer a "Show deleted" toggle on the emission-sources list page.
    /// </summary>
    public Task<PageResult<EmissionSource>> GetPagedIncludingDeletedAsync(Guid installationId,
        EmissionSourceType? type,
        int page, int pageSize, CancellationToken ct);

    Task<Option<EmissionSource>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Same as <see cref="GetByIdAsync"/> but bypasses the soft-delete filter so the
    /// frontend can render details for a deleted emission source and offer Restore.
    /// </summary>
    Task<Option<EmissionSource>> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken);
}
