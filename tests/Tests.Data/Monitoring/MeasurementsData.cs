using Domain.Entities.Monitoring;

namespace Tests.Data.Monitoring;

public static class MeasurementsData
{
    public static Measurement FirstSeedMeasurement(
        Guid emissionSourceId,
        Guid pollutantId,
        Guid deviceId,
        Guid unitId)
    {
        var end = DateTime.UtcNow.AddHours(-1);
        var start = end.AddHours(-1);
        return Measurement.New(
            id: Guid.NewGuid(),
            windowStart: start,
            windowEnd: end,
            window: AveragingWindow.Hour1,
            aggregation: Aggregation.Average,
            emissionSourceId: emissionSourceId,
            pollutantId: pollutantId,
            deviceId: deviceId,
            unitId: unitId,
            value: 10m,
            validPointsCount: 60,
            expectedPointsCount: 60);
    }
}
