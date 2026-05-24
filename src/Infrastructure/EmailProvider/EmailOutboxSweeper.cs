using Domain.Entities.Notifications;
using Hangfire;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.EmailProvider;

/// <summary>
/// Defense-in-depth backstop for the email outbox. Scans every minute for Pending rows that
/// the <see cref="EmailOutboxDispatchInterceptor"/> failed to schedule (Hangfire enqueue blip,
/// app process crash between commit and enqueue, etc.) and re-enqueues a processor job for each.
/// Idempotent against rows the interceptor did schedule — the processor's per-row tx + status
/// check makes duplicate enqueues safe.
/// </summary>
public class EmailOutboxSweeper(
    ApplicationDbContext context,
    IBackgroundJobClient jobs,
    ILogger<EmailOutboxSweeper> logger)
{
    private const int BatchSize = 200;
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(1);

    [AutomaticRetry(Attempts = 0)]
    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - StaleThreshold;
        var ids = await context.Set<EmailOutbox>().AsNoTracking()
            .Where(x => x.Status == EmailOutboxStatus.Pending && x.CreatedAt < cutoff)
            .OrderBy(x => x.CreatedAt)
            .Take(BatchSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (ids.Count == 0) return;

        foreach (var id in ids)
        {
            try
            {
                jobs.Enqueue<EmailOutboxProcessor>(p => p.ProcessAsync(id, CancellationToken.None));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sweeper failed to enqueue outbox row {OutboxId}", id);
            }
        }
        logger.LogInformation(
            "Email outbox sweep re-enqueued {Count} stale Pending row(s).", ids.Count);
    }
}
