using Domain.Common;
using Domain.Entities.EmissionSources;

namespace Domain.Entities.Monitoring;

public class DevicePollutantCapability : BaseEntity, ITenantOwned
{
    public Guid DeviceId { get; private set; }
    public MonitoringDevice? Device { get; private set; }
    public Guid EnterpriseId { get; private set; }
    public Guid PollutantId { get; private set; }
    public Pollutant? Pollutant { get; private set; }
    public decimal RangeMin { get; private set; }
    public decimal RangeMax { get; private set; }
    public Guid RangeUnitId { get; private set; }
    public MeasureUnit? RangeUnit { get; private set; }
    public string? AccuracyClass { get; private set; }

    /// <summary>
    /// How often the operator expects this device to ship a reading for this pollutant, in
    /// minutes. Drives the denominator of DataAvailability: a CEMS at 1/min sets 1 (default,
    /// matches IED Annex V), a periodic method (manual lab analysis, portable sampler) sets 5,
    /// 60, 1440, etc. so a slow-by-design device doesn't false-positive DataAvailabilityLoss
    /// every tick.
    /// </summary>
    public int ExpectedIntervalMinutes { get; private set; }

    private DevicePollutantCapability(Guid id, Guid deviceId, Guid pollutantId,
        decimal rangeMin, decimal rangeMax, Guid rangeUnitId, string? accuracyClass,
        int expectedIntervalMinutes)
    {
        Id = id;
        DeviceId = deviceId;
        PollutantId = pollutantId;
        RangeMin = rangeMin;
        RangeMax = rangeMax;
        RangeUnitId = rangeUnitId;
        AccuracyClass = accuracyClass;
        ExpectedIntervalMinutes = expectedIntervalMinutes;
    }

    public static DevicePollutantCapability New(Guid id, Guid deviceId, Guid pollutantId,
        decimal rangeMin, decimal rangeMax, Guid rangeUnitId, string? accuracyClass,
        int expectedIntervalMinutes = 1)
    {
        if (expectedIntervalMinutes < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedIntervalMinutes),
                expectedIntervalMinutes, "Expected interval must be at least 1 minute.");
        return new(id, deviceId, pollutantId, rangeMin, rangeMax, rangeUnitId, accuracyClass,
            expectedIntervalMinutes);
    }

    public void UpdateDetails(decimal rangeMin, decimal rangeMax, Guid rangeUnitId,
        string? accuracyClass, int expectedIntervalMinutes)
    {
        if (expectedIntervalMinutes < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedIntervalMinutes),
                expectedIntervalMinutes, "Expected interval must be at least 1 minute.");
        RangeMin = rangeMin;
        RangeMax = rangeMax;
        RangeUnitId = rangeUnitId;
        AccuracyClass = accuracyClass;
        ExpectedIntervalMinutes = expectedIntervalMinutes;
    }

    public void AssignTenant(Guid enterpriseId)
    {
        if (EnterpriseId == Guid.Empty)
        {
            EnterpriseId = enterpriseId;
        }
        else if (EnterpriseId != enterpriseId)
        {
            throw new InvalidOperationException(
                $"EnterpriseId is immutable on DevicePollutantCapability (current: {EnterpriseId}, attempted: {enterpriseId}).");
        }
    }
}
