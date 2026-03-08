using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Monitoring;

public interface IMonitoringPlanRepository
{
    Task<Option<MonitoringPlan>> GetByIdAsync(Guid id,
        CancellationToken cancellationToken);

    Task<Option<MonitoringPlan>> GetActiveByInstallationAsync(Guid installationId,
        CancellationToken cancellationToken);

    Task<Option<MonitoringPlan>> GetActiveByEmissionSourceAsync(Guid emissionSourceId,
        CancellationToken cancellationToken);

    MonitoringPlan Update(MonitoringPlan entity);
    Task<MonitoringPlan> AddAsync(MonitoringPlan entity, CancellationToken cancellationToken);

    MonitoringPlan Delete(MonitoringPlan entity);
}