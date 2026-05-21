namespace Application.Common.Settings;

/// <summary>
/// Which background runner schedules and executes the materializer + detectors. HostedService
/// is the legacy single-pass-per-tick model; Hangfire fans out one per-tenant job per tick so
/// a slow tenant only blocks its own worker. Defaults to HostedService until the Hangfire path
/// has soaked.
/// </summary>
public enum DetectionRunner
{
    HostedService = 0,
    Hangfire = 1
}

public class ComplianceDetectionSettings
{
    public bool Enabled { get; set; } = true;
    public int ScanIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Which runner schedules the periodic compliance work. Hot-swap by editing config —
    /// no code change required; service registration honours this at startup.
    /// </summary>
    public DetectionRunner Runner { get; set; } = DetectionRunner.HostedService;
    public int DeviceOfflineThresholdMinutes { get; set; } = 30;
    public decimal DataAvailabilityThreshold { get; set; } = 0.75m;
    public int MissingMeasurementWindowMinutes { get; set; } = 60;

    /// <summary>
    /// Upper bound on how far back materialization will reach when a tuple has no Measurement
    /// history. The actual start is max(limit.ValidFrom, now − MaxBackfillDays), so a new permit
    /// with a recent ValidFrom backfills only from that date, while a permit pointing far into
    /// the past is capped here to avoid scanning years of measurement_1m in one tick.
    /// </summary>
    public int MaxBackfillDays { get; set; } = 90;

    public int BackfillWindowsPerTick { get; set; } = 24;

    /// <summary>
    /// Number of most-recent closed windows the materializer re-evaluates on every tick to pick
    /// up late-arriving raw_measurement data (CEMS buffers, manual corrections, batch uploads).
    /// Scales with period — e.g. 6 hourly windows ≈ 6h tolerance; 6 minute windows ≈ 6min.
    /// If the existing Measurement's value/counts change, the record is updated in place and
    /// IED substitution is re-evaluated.
    /// </summary>
    public int LateArrivingRescanWindows { get; set; } = 6;

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

    /// <summary>
    /// Rolling window the OutOfRangeReading detector inspects for invalid raw_measurement rows.
    /// </summary>
    public int OutOfRangeWindowMinutes { get; set; } = 60;

    /// <summary>
    /// Fraction of invalid (out-of-range) raw rows per (source, device, pollutant) over the
    /// window above which an OutOfRangeReading event is opened. 0.10 = 10%.
    /// </summary>
    public decimal OutOfRangeThreshold { get; set; } = 0.10m;

    /// <summary>
    /// Minimum total number of raw rows in the window before the OutOfRangeReading detector
    /// will react — avoids flapping events when only a handful of points are present.
    /// </summary>
    public int OutOfRangeMinSampleCount { get; set; } = 10;
}
