using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Enterprises;

public interface ISiteQueries
{
    Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Site>> GetByEnterpriseIdAsync(Guid enterpriseId, CancellationToken cancellationToken);
    Task<Option<Site>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Option<Site>> GetByIdWithInstallationsAsync(Guid id, CancellationToken cancellationToken);
}