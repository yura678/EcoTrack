namespace Infrastructure.Compliance.Hangfire;

/// <summary>
/// Discriminator for the per-tenant detection job. Each kind maps to one of the existing
/// service entry points and runs on its own cadence (Fast every few minutes; AnnualLoad +
/// Calibration daily-ish). Used as part of the advisory-lock key so different kinds of the
/// same enterprise don't block each other.
/// </summary>
public enum DetectionJobKind
{
    Fast = 1,
    AnnualLoad = 2,
    Calibration = 3
}
