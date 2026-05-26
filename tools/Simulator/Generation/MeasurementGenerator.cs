using Simulator.Config;

namespace Simulator.Generation;

public sealed class MeasurementGenerator(GaussianRandom rng)
{
    public List<RawMeasurementDto> BuildBatch(DeviceProfile device, DateTime utcNow)
    {
        var batch = new List<RawMeasurementDto>(device.Pollutants.Count);
        var hourOfDay = utcNow.Hour + utcNow.Minute / 60.0;
        var diurnal = 1.0 + 0.25 * Math.Sin(2 * Math.PI * (hourOfDay - 6) / 24.0);

        foreach (var p in device.Pollutants)
        {
            var quality = "Valid";
            var roll = rng.NextUniform();
            if (roll < 0.05)
            {
                quality = rng.NextUniform() < 0.6 ? "Calibration" : "Maintenance";
            }

            var mean = (double)p.Mean * diurnal;
            var sample = rng.NextGaussian(mean, (double)p.Stdev);
            var clamped = Math.Clamp(
                sample,
                (double)p.RangeMin + 0.5,
                (double)p.RangeMax - 0.5);

            batch.Add(new RawMeasurementDto(
                Time: utcNow,
                EmissionSourceId: device.EmissionSourceId,
                PollutantId: p.PollutantId,
                UnitId: p.UnitId,
                RawValue: (decimal)Math.Round(clamped, 3),
                Quality: quality));
        }

        return batch;
    }
}

public sealed record RawMeasurementDto(
    DateTime Time,
    Guid EmissionSourceId,
    Guid PollutantId,
    Guid UnitId,
    decimal RawValue,
    string Quality);
