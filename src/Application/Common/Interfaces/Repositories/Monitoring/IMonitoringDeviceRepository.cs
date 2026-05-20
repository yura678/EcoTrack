using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Monitoring;

public interface IMonitoringDeviceRepository
{
    Task<Option<MonitoringDevice>> GetByIdAsync(Guid emissionSourceId,
        Guid id, CancellationToken cancellationToken);

    Task<Option<MonitoringDevice>> GetByIdAsync(
        Guid id, CancellationToken cancellationToken);

    Task<Option<MonitoringDevice>> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken);
    Task<Option<MonitoringDevice>> GetBySerialNumberIncludingDeletedAsync(string serialNumber, CancellationToken cancellationToken);
    Task<MonitoringDevice> AddAsync(MonitoringDevice entity, CancellationToken cancellationToken);
    MonitoringDevice Update(MonitoringDevice entity);
    MonitoringDevice Delete(MonitoringDevice entity);
    public Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MonitoringDevice>> GetByInstallationIdsAsync(IReadOnlyList<Guid> installationIds,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<MonitoringDevice>> GetByEmissionSourceIdAsync(Guid emissionSourceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads devices of the installation whose Status is anything but Decommissioned, tracked
    /// so the caller can mutate them and SaveChanges in the same UoW. Used by the
    /// installation-decommission cascade.
    /// </summary>
    Task<IReadOnlyList<MonitoringDevice>> GetNonDecommissionedByInstallationAsync(
        Guid installationId, CancellationToken cancellationToken);
}