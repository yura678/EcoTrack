using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record CreateDevicePollutantCapabilityDto(
    Guid PollutantId,
    decimal RangeMin,
    decimal RangeMax,
    Guid RangeUnitId,
    string? AccuracyClass,
    int ExpectedIntervalMinutes = 1);

public record UpdateDevicePollutantCapabilityDto(
    decimal RangeMin,
    decimal RangeMax,
    Guid RangeUnitId,
    string? AccuracyClass,
    int ExpectedIntervalMinutes = 1);

public record DevicePollutantCapabilityDto(
    Guid Id,
    Guid DeviceId,
    Guid PollutantId,
    decimal RangeMin,
    decimal RangeMax,
    Guid RangeUnitId,
    string? AccuracyClass,
    int ExpectedIntervalMinutes)
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
            capability.AccuracyClass,
            capability.ExpectedIntervalMinutes
        );
    }
}
