using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Queries.Notifications;
using Domain.Entities.Monitoring;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Compliance.Notifications;

/// <summary>
/// Hangfire fan-out job for one ComplianceEvent. Loads the event, finds matching
/// subscriptions for the event's enterprise, and enqueues one
/// <see cref="PerSubscriptionNotificationDispatcher"/> job per subscription. Each per-sub job
/// owns its own retry budget and delivery audit row, so one bad webhook URL no longer
/// silences itself (Phase A) or blocks the dispatch for everyone else.
/// <para>
/// This job intentionally does no I/O beyond reading subscriptions and enqueuing Hangfire
/// children. The whole job is short — if it fails, Hangfire's <c>[AutomaticRetry]</c>
/// re-runs the fan-out and the per-sub job's <c>(SubscriptionId, ComplianceEventId)</c>
/// idempotency key absorbs the duplicate enqueues.
/// </para>
/// </summary>
public class ComplianceNotificationDispatcher(
    IComplianceEventQueries complianceEventQueries,
    INotificationSubscriptionQueries subscriptionQueries,
    IBackgroundJobClient jobClient,
    ILogger<ComplianceNotificationDispatcher> logger)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 30, 120, 600, 1800, 3600 })]
    public async Task DispatchAsync(Guid complianceEventId, CancellationToken cancellationToken)
    {
        var eventOption = await complianceEventQueries.GetByIdAsync(complianceEventId, cancellationToken);
        await eventOption.Match(
            Some: ev => FanOutAsync(ev, cancellationToken),
            None: () =>
            {
                logger.LogWarning(
                    "Compliance event {EventId} not found — dispatcher skipped",
                    complianceEventId);
                return Task.CompletedTask;
            });
    }

    private async Task FanOutAsync(ComplianceEvent complianceEvent, CancellationToken cancellationToken)
    {
        var subscriptions = await subscriptionQueries.GetMatchingForEventAsync(
            complianceEvent.EnterpriseId,
            complianceEvent.EventType,
            complianceEvent.EmissionSourceId,
            cancellationToken);

        if (subscriptions.Count == 0)
        {
            logger.LogDebug(
                "No matching subscriptions for event {EventId} ({EventType}/{SourceId})",
                complianceEvent.Id, complianceEvent.EventType, complianceEvent.EmissionSourceId);
            return;
        }

        foreach (var sub in subscriptions)
        {
            // CancellationToken.None for the child job: Hangfire serialises the call and
            // re-creates the token at execution time. Passing the master's token would crash
            // the serializer (CancellationToken is not serialisable).
            jobClient.Enqueue<PerSubscriptionNotificationDispatcher>(
                d => d.DispatchAsync(complianceEvent.Id, sub.Id, CancellationToken.None));
        }

        logger.LogInformation(
            "Fanned out compliance event {EventId} to {Count} subscription(s).",
            complianceEvent.Id, subscriptions.Count);
    }
}
