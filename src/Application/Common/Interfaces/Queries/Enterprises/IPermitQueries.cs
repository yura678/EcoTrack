using Application.Common.Models;
using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Enterprises;

public interface IPermitQueries
{
    Task<Option<Permit>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PageResult<Permit>> GetPagedAsync(Guid installationId,
        DateTime? from, DateTime? to, int page, int pageSize,
        CancellationToken cancellationToken);
}