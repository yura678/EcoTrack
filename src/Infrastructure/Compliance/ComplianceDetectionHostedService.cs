using Application.Common.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Compliance;

public class ComplianceDetectionHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<ComplianceDetectionSettings> options,
    ILogger<ComplianceDetectionHostedService> logger) : BackgroundService
{
    private readonly ComplianceDetectionSettings _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            logger.LogInformation("ComplianceDetectionHostedService is disabled.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _settings.ScanIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        logger.LogInformation(
            "ComplianceDetectionHostedService starting; tick interval {Interval}", interval);

        // Initial delay so we don't compete with startup migrations / seed.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var materialization = scope.ServiceProvider
                    .GetRequiredService<MeasurementMaterializationService>();
                await materialization.RunAsync(stoppingToken);

                var detection = scope.ServiceProvider
                    .GetRequiredService<ComplianceDetectionService>();
                await detection.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Compliance detection cycle failed");
            }
        } while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
