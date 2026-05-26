using Simulator.Config;

namespace Simulator.Generation;

public sealed class ProcessParameterGenerator(GaussianRandom rng)
{
    public List<RawProcessParameterDto> BuildBatch(DeviceProfile device, DateTime utcNow)
    {
        var batch = new List<RawProcessParameterDto>(device.Parameters.Count);
        foreach (var p in device.Parameters)
        {
            var quality = rng.NextUniform() < 0.02 ? "Maintenance" : "Valid";
            var sample = rng.NextGaussian((double)p.Mean, (double)p.Stdev);

            batch.Add(new RawProcessParameterDto(
                Time: utcNow,
                EmissionSourceId: device.EmissionSourceId,
                ParameterType: p.Type,
                Value: (decimal)Math.Round(sample, 3),
                UnitId: p.UnitId,
                Quality: quality));
        }
        return batch;
    }
}

public sealed record RawProcessParameterDto(
    DateTime Time,
    Guid EmissionSourceId,
    string ParameterType,
    decimal Value,
    Guid UnitId,
    string Quality);
