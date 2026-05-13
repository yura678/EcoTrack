using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record CreateDevicePollutantCapabilityDto(
    Guid PollutantId,
    decimal RangeMin,
    decimal RangeMax,
    Guid RangeUnitId,
    string? AccuracyClass);

public record UpdateDevicePollutantCapabilityDto(
    decimal RangeMin,
    decimal RangeMax,
    Guid RangeUnitId,
    string? AccuracyClass);

public record DevicePollutantCapabilityDto(
    Guid Id,
    Guid DeviceId,
    Guid PollutantId,
    decimal RangeMin,
    decimal RangeMax,
    Guid RangeUnitId,
    string? AccuracyClass)
{
    public static DevicePollutantCapabilityDto FromDomainModel(DevicePollutantCapability capability)
    {
        return new DevicePollutantCapabilityDto(
            capability.Id,
            capability.DeviceId,
            capability.PollutantId,
            capability.RangeMin,
            capability.RangeMax,
            capability.RangeUnitId,
            capability.AccuracyClass
        );
    }
}
