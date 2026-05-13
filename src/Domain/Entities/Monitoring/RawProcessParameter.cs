namespace Domain.Entities.Monitoring;

/// <summary>
/// Immutable raw reading of a gas flow / process parameter (temperature, pressure, O2, flow rate, etc.)
/// taken alongside pollutant measurements. Not subject to EmissionLimit — only inputs for normalization
/// and mass-flow calculation. Becomes a Timescale hypertable in the real-time pipeline.
/// </summary>
public class RawProcessParameter
{
    public long Id { get; private set; }
    public DateTime Time { get; private set; }
    public Guid EmissionSourceId { get; private set; }
    public Guid DeviceId { get; private set; }
    public ParameterType ParameterType { get; private set; }
    public decimal Value { get; private set; }
    public Guid UnitId { get; private set; }
    public Quality Quality { get; private set; }
    public DateTime IngestionTime { get; private set; }

    private RawProcessParameter(DateTime time, Guid emissionSourceId, Guid deviceId,
        ParameterType parameterType, decimal value, Guid unitId, Quality quality, DateTime ingestionTime)
    {
        Time = time;
        EmissionSourceId = emissionSourceId;
        DeviceId = deviceId;
        ParameterType = parameterType;
        Value = value;
        UnitId = unitId;
        Quality = quality;
        IngestionTime = ingestionTime;
    }

    public static RawProcessParameter New(DateTime time, Guid emissionSourceId, Guid deviceId,
        ParameterType parameterType, decimal value, Guid unitId, Quality quality = Quality.Valid) =>
        new(time, emissionSourceId, deviceId, parameterType, value, unitId, quality, DateTime.UtcNow);
}
