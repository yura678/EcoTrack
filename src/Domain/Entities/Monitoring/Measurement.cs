using Domain.Common;
using Domain.Entities.EmissionSources;

namespace Domain.Entities.Monitoring;

public class Measurement : BaseEntity
{
    public DateTime Timestamp { get; }
    public Guid EmissionSourceId { get; private set; }
    public EmissionSource? EmissionSource { get; private set; }

    public Guid PollutantId { get; private set; }
    public Pollutant? Pollutant { get; private set; }

    public Guid DeviceId { get; private set; }
    public MonitoringDevice? Device { get; private set; }

    public Guid UnitId { get; private set; }
    public MeasureUnit? Unit { get; private set; }

    public AveragingWindow Period { get; private set; }

    public decimal Value { get; private set; }

    public MeasurementStatus Status { get; private set; }
    public string? Reason { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime? UpdatedAt { get; private set; }

    private Measurement(Guid id, DateTime timestamp, Guid emissionSourceId,
        Guid pollutantId, Guid deviceId, Guid unitId,
        AveragingWindow period, decimal value, MeasurementStatus status, string? reason, DateTime createdAt,
        DateTime? updatedAt)
    {
        Id = id;
        Timestamp = timestamp;
        EmissionSourceId = emissionSourceId;
        PollutantId = pollutantId;
        DeviceId = deviceId;
        UnitId = unitId;
        Period = period;
        Value = value;
        Status = status;
        Reason = reason;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Measurement New(Guid id, DateTime timestamp, Guid emissionSourceId,
        Guid pollutantId, Guid deviceId, Guid unitId,
        AveragingWindow period, decimal value, string? reason) =>
        new(id, timestamp, emissionSourceId, pollutantId, deviceId, unitId, period, value, MeasurementStatus.Valid,
            reason, DateTime.UtcNow, null);


    public void ChangeStatus(MeasurementStatus status, string? reason)
    {
        Status = status;
        Reason = reason;

        UpdatedAt = DateTime.UtcNow;
    }
}

public enum AveragingWindow // для Measurement і Limit
{
    OneHour, // 1h
    TwentyFourHours // 24h
}

public enum MeasurementStatus
{
    Valid = 0,
    Rejected = 1
}