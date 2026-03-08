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

    Task<Option<EmissionSource>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}