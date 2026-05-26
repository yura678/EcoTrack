namespace Simulator.Config;

public sealed class SimulatorConfig
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5269";
    public int IntervalSeconds { get; set; } = 10;
    public bool IncludeOfflineDevices { get; set; } = false;
    public List<DeviceProfile> Devices { get; set; } = new();
}

public sealed class DeviceProfile
{
    public string Serial { get; set; } = string.Empty;
    public string IngestionSecret { get; set; } = string.Empty;
    public Guid EnterpriseId { get; set; }
    public Guid EmissionSourceId { get; set; }
    public List<PollutantProfile> Pollutants { get; set; } = new();
    public List<ParameterProfile> Parameters { get; set; } = new();
}

public sealed class PollutantProfile
{
    public Guid PollutantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid UnitId { get; set; }
    public decimal Mean { get; set; }
    public decimal Stdev { get; set; }
    public decimal RangeMin { get; set; }
    public decimal RangeMax { get; set; }
}

public sealed class ParameterProfile
{
    public string Type { get; set; } = string.Empty;
    public Guid UnitId { get; set; }
    public decimal Mean { get; set; }
    public decimal Stdev { get; set; }
}
