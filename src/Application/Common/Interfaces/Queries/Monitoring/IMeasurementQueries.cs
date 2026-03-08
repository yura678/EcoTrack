using Application.Common.Models;
using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Monitoring;

public interface IMeasurementQueries
{
    Task<Option<Measurement>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PageResult<Measurement>> GetPagedAsync(Guid installationId, DateTime? from,
        DateTime? to, int page, int pageSize, CancellationToken cancellationToken);
}