using Application.Common.Models;
using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Monitoring;

public interface IMonitoringPlanQueries
{
    Task<Option<MonitoringPlan>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PageResult<MonitoringPlan>> GetPagedAsync(Guid installationId, DateTime? from,
        DateTime? to, int page, int pageSize,
        CancellationToken cancellationToken);
}