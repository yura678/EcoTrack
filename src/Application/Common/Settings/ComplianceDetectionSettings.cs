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

    /// <summary>
    /// Suppress DeviceOffline and no-calibration alerts for devices installed within this window.
    /// Gives operators time to commission a device + record initial calibration without false alarms.
    /// </summary>
    public int NewDeviceGraceDays { get; set; } = 7;

    /// <summary>
    /// Cadence for AnnualLoad detection. Annual/monthly running averages change very slowly,
    /// so running them every fast tick wastes CPU and DB I/O.
    /// </summary>
    public int AnnualLoadIntervalHours { get; set; } = 24;

    /// <summary>
    /// Cadence for calibration-status checks. Calibration records are added weekly/monthly
    /// and overdue status only crosses once per day, so checking every fast tick is wasteful.
    /// </summary>
    public int CalibrationCheckIntervalHours { get; set; } = 6;

    /// <summary>
    /// IED substitution: when DataAvailability drops below threshold, replace the computed
    /// value with the maximum observed in the last N valid windows times this multiplier.
    /// 1.05 is the customary conservative bump per CEN/TS recommendations.
    /// </summary>
    public decimal SubstitutionMultiplier { get; set; } = 1.05m;

    /// <summary>
    /// How many prior valid windows to scan for the substitution maximum.
    /// </summary>
    public int SubstitutionLookbackWindows { get; set; } = 30;
}
