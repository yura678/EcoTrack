using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Monitoring;

public interface IDevicePollutantCapabilityQueries
{
    Task<IReadOnlyList<DevicePollutantCapability>> GetByDeviceIdAsync(Guid deviceId,
        CancellationToken cancellationToken);
    Task<Option<DevicePollutantCapability>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
