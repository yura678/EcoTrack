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
}