namespace Application.Common.Settings;

public class ComplianceDetectionSettings
{
    public bool Enabled { get; set; } = true;
    public int ScanIntervalMinutes { get; set; } = 5;
    public int DeviceOfflineThresholdMinutes { get; set; } = 30;
    public decimal DataAvailabilityThreshold { get; set; } = 0.75m;
    public int MissingMeasurementWindowMinutes { get; set; } = 60;
    public int BackfillDays { get; set; } = 3;
    public int BackfillWindowsPerTick { get; set; } = 24;
}
