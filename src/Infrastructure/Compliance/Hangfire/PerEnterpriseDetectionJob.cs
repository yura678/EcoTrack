using global::Hangfire;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Compliance.Hangfire;

/// <summary>
/// Hangfire-invocable unit that runs materialization + detection for one tenant. Sits at the
/// leaf of the per-enterprise fan-out: the master <see cref="DetectionScheduler"/> enqueues
/// one of these per (tenant, kind) per tick, the Hangfire worker pool picks them up, and a
/// per-(kind, tenant) Postgres advisory lock prevents the same tuple from overlapping if the
/// previous run still hasn't finished.
///
/// The "noisy neighbour" problem the legacy hosted services have — one slow tenant blocks
/// every other tenant on the tick — is solved here because each job binds to its own worker
/// only.
/// </summary>
public class PerEnterpriseDetectionJob(
    MeasurementMaterializationService materializer,
    ComplianceDetectionService detector,
    DetectionLockProvider lockProvider,
    ILogger<PerEnterpriseDetectionJob> logger)
{
    // Knuth's golden-ratio multiplier — used to scatter (kind, enterprise) pairs across the
    // 64-bit advisory-lock key space so different kinds for the same tenant don't collide.
    private const long KindMultiplier = unchecked((long)0x9E3779B97F4A7C15);

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 30, 300 })]
    public async Task ExecuteAsync(
        Guid enterpriseId, DetectionJobKind kind, CancellationToken cancellationToken)
    {
        var lockKey = ComputeLockKey(kind, enterpriseId);
        await using var lockHandle = await lockProvider.TryAcquireAsync(lockKey, cancellationToken);
        if (lockHandle is null)
        {
            logger.LogInformation(
                "Skipped {Kind} for {EnterpriseId}: previous run still in flight.",
                kind, enterpriseId);
            return;
        }

        switch (kind)
        {
            case DetectionJobKind.Fast:
                // Materialization MUST precede fast detection — detectors read what the
                // materializer just wrote in this tick.
                await materializer.RunForEnterpriseAsync(enterpriseId, cancellationToken);
                await detector.RunForEnterpriseAsync(enterpriseId, cancellationToken);
                break;

            case DetectionJobKind.AnnualLoad:
                await detector.RunAnnualLoadForEnterpriseAsync(enterpriseId, cancellationToken);
                break;

            case DetectionJobKind.Calibration:
                await detector.RunCalibrationChecksForEnterpriseAsync(enterpriseId, cancellationToken);
                break;

            default:
                logger.LogWarning("Unknown DetectionJobKind {Kind} for {EnterpriseId}; skipping.",
                    kind, enterpriseId);
                break;
        }
    }

    /// <summary>
    /// Per-(kind, enterprise) Postgres advisory-lock key. Uses the first 8 bytes of the Guid
    /// XOR'd with a per-kind constant so two kinds for the same tenant occupy different keys
    /// and don't block each other.
    /// </summary>
    public static long ComputeLockKey(DetectionJobKind kind, Guid enterpriseId)
    {
        var bytes = enterpriseId.ToByteArray();
        var enterpriseBits = BitConverter.ToInt64(bytes, 0);
        return enterpriseBits ^ ((long)kind * KindMultiplier);
    }
}
