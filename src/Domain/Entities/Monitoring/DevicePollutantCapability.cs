using Domain.Common;
using Domain.Entities.EmissionSources;

namespace Domain.Entities.Monitoring;

public class DevicePollutantCapability : BaseEntity
{
    public Guid DeviceId { get; private set; }
    public MonitoringDevice? Device { get; private set; }
    public Guid PollutantId { get; private set; }
    public Pollutant? Pollutant { get; private set; }
    public decimal RangeMin { get; private set; }
    public decimal RangeMax { get; private set; }
    public Guid RangeUnitId { get; private set; }
    public MeasureUnit? RangeUnit { get; private set; }
    public string? AccuracyClass { get; private set; }

    private DevicePollutantCapability(Guid id, Guid deviceId, Guid pollutantId,
        decimal rangeMin, decimal rangeMax, Guid rangeUnitId, string? accuracyClass)
    {
        Id = id;
        DeviceId = deviceId;
        PollutantId = pollutantId;
        RangeMin = rangeMin;
        RangeMax = rangeMax;
        RangeUnitId = rangeUnitId;
        AccuracyClass = accuracyClass;
    }

    public static DevicePollutantCapability New(Guid id, Guid deviceId, Guid pollutantId,
        decimal rangeMin, decimal rangeMax, Guid rangeUnitId, string? accuracyClass) =>
        new(id, deviceId, pollutantId, rangeMin, rangeMax, rangeUnitId, accuracyClass);

    public void UpdateDetails(decimal rangeMin, decimal rangeMax, Guid rangeUnitId,
        string? accuracyClass)
    {
        RangeMin = rangeMin;
        RangeMax = rangeMax;
        RangeUnitId = rangeUnitId;
        AccuracyClass = accuracyClass;
    }
}
