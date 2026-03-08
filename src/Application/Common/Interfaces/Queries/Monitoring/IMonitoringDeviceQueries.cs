using Application.Common.Models;
using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Monitoring;

public interface IMonitoringDeviceQueries
{
    Task<IReadOnlyList<MonitoringDevice>> GetAllAsync(CancellationToken cancellationToken);

    public Task<PageResult<MonitoringDevice>> GetPagedAsync(
        Guid installationId, int page, int pageSize, CancellationToken cancellationToken);

    Task<Option<MonitoringDevice>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}