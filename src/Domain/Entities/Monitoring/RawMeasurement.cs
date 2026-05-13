namespace Domain.Entities.Monitoring;

/// <summary>
/// Immutable raw measurement point as produced by a monitoring device. Becomes a Timescale hypertable
/// when the real-time ingestion pipeline is enabled. Uses bigserial PK (not Guid) for hypertable efficiency.
/// </summary>
public class RawMeasurement
{
    public long Id { get; private set; }
    public DateTime Time { get; private set; }
    public Guid EmissionSourceId { get; private set; }
    public Guid PollutantId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid UnitId { get; private set; }
    public decimal RawValue { get; private set; }
    public Quality Quality { get; private set; }
    public DateTime IngestionTime { get; private set; }

    private RawMeasurement(DateTime time, Guid emissionSourceId, Guid pollutantId, Guid deviceId,
        Guid unitId, decimal rawValue, Quality quality, DateTime ingestionTime)
    {
        Time = time;
        EmissionSourceId = emissionSourceId;
        PollutantId = pollutantId;
        DeviceId = deviceId;
        UnitId = unitId;
        RawValue = rawValue;
        Quality = quality;
        IngestionTime = ingestionTime;
    }

    public static RawMeasurement New(DateTime time, Guid emissionSourceId, Guid pollutantId,
        Guid deviceId, Guid unitId, decimal rawValue, Quality quality = Quality.Valid) =>
        new(time, emissionSourceId, pollutantId, deviceId, unitId, rawValue, quality, DateTime.UtcNow);
}
