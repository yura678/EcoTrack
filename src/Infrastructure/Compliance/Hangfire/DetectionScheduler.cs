using Application.Common.Interfaces.Queries.Enterprises;
using global::Hangfire;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Compliance.Hangfire;

/// <summary>
/// Master scheduler — invoked by Hangfire's recurring-job mechanism on each cadence and
/// fans out one <see cref="PerEnterpriseDetectionJob"/> per active tenant. Doesn't do any
/// detection work itself; just lists tenants and enqueues.
///
/// AutomaticRetry is disabled because the next scheduled tick will pick up where this one
/// left off — retrying the master would amplify load when the DB is the actual problem.
/// </summary>
public class DetectionScheduler(
    IEnterpriseQueries enterpriseQueries,
    IBackgroundJobClient jobs,
    ILogger<DetectionScheduler> logger)
{
    [AutomaticRetry(Attempts = 0)]
    public Task ScheduleFastAsync(CancellationToken cancellationToken) =>
        ScheduleKindAsync(DetectionJobKind.Fast, cancellationToken);

    [AutomaticRetry(Attempts = 0)]
    public Task ScheduleAnnualLoadAsync(CancellationToken cancellationToken) =>
        ScheduleKindAsync(DetectionJobKind.AnnualLoad, cancellationToken);

    [AutomaticRetry(Attempts = 0)]
    public Task ScheduleCalibrationAsync(CancellationToken cancellationToken) =>
        ScheduleKindAsync(DetectionJobKind.Calibration, cancellationToken);

    private async Task ScheduleKindAsync(DetectionJobKind kind, CancellationToken cancellationToken)
    {
        var enterpriseIds = await enterpriseQueries.GetActiveEnterpriseIdsAsync(cancellationToken);
        if (enterpriseIds.Count == 0)
        {
            logger.LogInformation("No active enterprises; {Kind} schedule is a no-op.", kind);
            return;
        }

        foreach (var enterpriseId in enterpriseIds)
        {
            // CancellationToken.None for the child job: Hangfire serialises the call and
            // re-creates the token at execution time. Passing the master's token would
            // crash the serializer (CancellationToken is not serialisable).
            jobs.Enqueue<PerEnterpriseDetectionJob>(
                job => job.ExecuteAsync(enterpriseId, kind, CancellationToken.None));
        }

        logger.LogInformation(
            "Enqueued {Kind} job for {Count} enterprise(s).", kind, enterpriseIds.Count);
    }
}
