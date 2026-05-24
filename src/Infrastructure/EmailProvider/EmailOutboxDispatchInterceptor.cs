using Domain.Entities.Notifications;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Infrastructure.EmailProvider;

/// <summary>
/// Captures newly-added <see cref="EmailOutbox"/> entities during <c>SaveChanges</c> and, once
/// the save has flushed successfully, schedules a Hangfire job to process each one. The schedule
/// uses a small delay so the worker can't beat an outer user transaction's commit — if a caller
/// is in <c>BeginTransaction → SaveChanges → … → Commit</c>, <c>SavedChanges</c> fires after the
/// flush but BEFORE the commit, and a zero-delay Enqueue could race the worker into reading the
/// row before it's visible. The 2-second delay buys time; if the outer transaction rolls back
/// the worker finds the row missing and no-ops.
/// </summary>
public class EmailOutboxDispatchInterceptor(
    IBackgroundJobClient jobs,
    ILogger<EmailOutboxDispatchInterceptor> logger) : SaveChangesInterceptor
{
    // Scoped per DbContext (registered scoped). HashSet so duplicate SavingChanges callbacks
    // (EF can invoke an interceptor more than once per save when host wiring registers options
    // configurations from multiple layers) don't double-enqueue the same row.
    private readonly HashSet<Guid> _pendingDispatch = [];

    // Async-only: every SaveChanges in the project goes through SaveChangesAsync. Overriding
    // the sync versions in addition would double-fire on instrumentation paths that probe both.
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        Dispatch();
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendingDispatch.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;
        foreach (var entry in context.ChangeTracker.Entries<EmailOutbox>())
        {
            if (entry.State == EntityState.Added
                && entry.Entity.Status == EmailOutboxStatus.Pending)
            {
                _pendingDispatch.Add(entry.Entity.Id);
            }
        }
    }

    private void Dispatch()
    {
        if (_pendingDispatch.Count == 0) return;
        foreach (var id in _pendingDispatch)
        {
            try
            {
                jobs.Schedule<EmailOutboxProcessor>(
                    p => p.ProcessAsync(id, CancellationToken.None),
                    TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                // Hangfire enqueue failure isn't fatal — the recurring sweeper picks up stale
                // Pending rows. Log so an ops alert can spot Hangfire-side outages.
                logger.LogError(ex,
                    "Failed to schedule Hangfire processor for outbox row {OutboxId}; " +
                    "the recurring sweeper will retry.", id);
            }
        }
        _pendingDispatch.Clear();
    }
}
