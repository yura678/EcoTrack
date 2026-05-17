using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Compliance;

/// <summary>
/// Common loop for a compliance background job: PeriodicTimer + startup delay + advisory-lock
/// guard + per-tick scope. Subclasses provide cadence, lock key, and the body of one tick.
/// </summary>
public abstract class BaseDetectionHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger logger) : BackgroundService
{
    protected abstract string JobName { get; }
    protected abstract TimeSpan Interval { get; }
    protected abstract long LockKey { get; }
    protected virtual bool Enabled => true;
    protected virtual TimeSpan InitialDelay => TimeSpan.FromSeconds(30);

    protected abstract Task ExecuteTickAsync(IServiceScope scope, CancellationToken stoppingToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            logger.LogInformation("{Job} hosted service disabled.", JobName);
            return;
        }

        var interval = Interval;
        using var timer = new PeriodicTimer(interval);
        logger.LogInformation("{Job} hosted service starting; tick interval {Interval}.", JobName, interval);

        // Avoid competing with startup migrations / seed.
        try { await Task.Delay(InitialDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var lockProvider = scope.ServiceProvider.GetRequiredService<DetectionLockProvider>();

                await using var lockHandle = await lockProvider.TryAcquireAsync(LockKey, stoppingToken);
                if (lockHandle is null)
                {
                    logger.LogDebug("Another instance holds the {Job} lock; skipping tick.", JobName);
                    continue;
                }

                await ExecuteTickAsync(scope, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Job} cycle failed.", JobName);
            }
        } while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
