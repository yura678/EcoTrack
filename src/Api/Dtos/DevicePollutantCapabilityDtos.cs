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
    PollutantDto? Pollutant,
    decimal RangeMin,
    decimal RangeMax,
    Guid RangeUnitId,
    MeasureUnitDto? RangeUnit,
    string? AccuracyClass,
    int ExpectedIntervalMinutes)
{
    public static DevicePollutantCapabilityDto FromDomainModel(DevicePollutantCapability capability)
    {
        return new DevicePollutantCapabilityDto(
            capability.Id,
            capability.DeviceId,
            capability.PollutantId,
            capability.Pollutant is not null ? PollutantDto.FromDomainModel(capability.Pollutant) : null,
            capability.RangeMin,
            capability.RangeMax,
            capability.RangeUnitId,
            capability.RangeUnit is not null ? MeasureUnitDto.FromDomainModel(capability.RangeUnit) : null,
            capability.AccuracyClass,
            capability.ExpectedIntervalMinutes
        );
    }
}
