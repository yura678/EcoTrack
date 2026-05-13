using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Monitoring;

public interface IDevicePollutantCapabilityRepository
{
    Task<Option<DevicePollutantCapability>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Option<DevicePollutantCapability>> GetByDeviceAndPollutantAsync(Guid deviceId,
        Guid pollutantId, CancellationToken cancellationToken);
    Task<DevicePollutantCapability> AddAsync(DevicePollutantCapability entity,
        CancellationToken cancellationToken);
    DevicePollutantCapability Update(DevicePollutantCapability entity);
    DevicePollutantCapability Delete(DevicePollutantCapability entity);
}
