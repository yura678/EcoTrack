namespace Simulator.Generation;

public sealed class GaussianRandom
{
    private readonly Random _rng;
    private double? _spare;

    public GaussianRandom(int? seed = null)
    {
        _rng = seed is null ? Random.Shared : new Random(seed.Value);
    }

    public double NextStandard()
    {
        if (_spare.HasValue)
        {
            var v = _spare.Value;
            _spare = null;
            return v;
        }

        double u1, u2;
        do { u1 = _rng.NextDouble(); } while (u1 <= double.Epsilon);
        u2 = _rng.NextDouble();

        var mag = Math.Sqrt(-2.0 * Math.Log(u1));
        var z0 = mag * Math.Cos(2.0 * Math.PI * u2);
        var z1 = mag * Math.Sin(2.0 * Math.PI * u2);
        _spare = z1;
        return z0;
    }

    public double NextGaussian(double mean, double stdev) => mean + stdev * NextStandard();

    public double NextUniform() => _rng.NextDouble();
}
