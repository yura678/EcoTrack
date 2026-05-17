using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Monitoring;

public interface IDevicePollutantCapabilityQueries
{
    Task<IReadOnlyList<DevicePollutantCapability>> GetByDeviceIdAsync(Guid deviceId,
        CancellationToken cancellationToken);
    Task<Option<DevicePollutantCapability>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// For a single device, returns the subset of requested PollutantIds that the device has
    /// an active capability for. The ingest gate uses this to reject batches that reference
    /// pollutants the device is not configured to measure.
    /// </summary>
    Task<System.Collections.Generic.HashSet<Guid>> GetConfiguredPollutantsForDeviceAsync(
        Guid deviceId,
        IReadOnlyCollection<Guid> pollutantIds,
        CancellationToken cancellationToken);
}
