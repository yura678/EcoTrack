using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Monitoring;

/// <summary>
/// Range + unit metadata for a single (device, pollutant) capability — projected so callers
/// don't have to load the full DevicePollutantCapability entity.
/// </summary>
public record DeviceCapabilityRange(
    Guid PollutantId,
    decimal RangeMin,
    decimal RangeMax,
    Guid RangeUnitId);

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

    /// <summary>
    /// Loads range + unit metadata for each requested (device, pollutantId) pair. Used by the
    /// ingest range-check to decide whether an incoming rawValue lies inside the sensor's
    /// declared shading.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, DeviceCapabilityRange>> GetCapabilityRangesForDeviceAsync(
        Guid deviceId,
        IReadOnlyCollection<Guid> pollutantIds,
        CancellationToken cancellationToken);
}
