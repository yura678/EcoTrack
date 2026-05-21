using Application.Common.Models;
using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Enterprises;

public interface IEnterpriseQueries
{
    Task<IReadOnlyList<Enterprise>> GetAllAsync(CancellationToken cancellationToken);

    public Task<PageResult<Enterprise>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<Option<Enterprise>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Option<Enterprise>> GetByIdWithSitesAsync(Guid id, CancellationToken cancellationToken);
    Task<Option<Enterprise>> GetByEdrpouAsync(string edrpou, CancellationToken cancellationToken);

    /// <summary>
    /// Lightweight projection of every non-soft-deleted enterprise's Id — used by the
    /// per-tenant detection scheduler to fan out one Hangfire job per tenant per tick.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetActiveEnterpriseIdsAsync(CancellationToken cancellationToken);
}