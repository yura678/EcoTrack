using Domain.Entities.Monitoring;

namespace Tests.Data.Monitoring;

public static class MeasurementsData
{
    public static Measurement FirstSeedMeasurement(
       Guid emissionSourceId,
       Guid pollutantId,
       Guid deviceId,
       Guid unitId)
        => Measurement.New(
            id: Guid.NewGuid(), 
            timestamp: DateTime.UtcNow.AddHours(-2),
            emissionSourceId: emissionSourceId,
            pollutantId: pollutantId,
            deviceId: deviceId,
            unitId: unitId,
            period: AveragingWindow.OneHour,
            value: 10m,
            reason: null);
}