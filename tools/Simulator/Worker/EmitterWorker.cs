using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simulator.Config;
using Simulator.Generation;
using Simulator.Http;

namespace Simulator.Worker;

public sealed class EmitterWorker(
    SimulatorConfig config,
    HmacIngestClient ingestClient,
    MeasurementGenerator measurementGenerator,
    ProcessParameterGenerator parameterGenerator,
    IHostApplicationLifetime lifetime,
    ILogger<EmitterWorker> logger,
    EmitterOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (config.Devices.Count == 0)
        {
            logger.LogError("No devices in config; nothing to emit. Run `simulator bootstrap` first.");
            lifetime.StopApplication();
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, options.IntervalSeconds ?? config.IntervalSeconds));
        logger.LogInformation(
            "Streaming for {DeviceCount} devices every {IntervalSeconds}s (single-shot: {Once}).",
            config.Devices.Count, interval.TotalSeconds, options.RunOnce);

        using var timer = new PeriodicTimer(interval);
        do
        {
            var tickStart = DateTime.UtcNow;
            foreach (var device in config.Devices)
            {
                await EmitDeviceAsync(device, tickStart, stoppingToken);
            }

            if (options.RunOnce)
            {
                logger.LogInformation("--once requested; exiting.");
                lifetime.StopApplication();
                return;
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task EmitDeviceAsync(DeviceProfile device, DateTime utcNow, CancellationToken ct)
    {
        if (device.Pollutants.Count > 0)
        {
            var batch = measurementGenerator.BuildBatch(device, utcNow);
            await SafePostAsync("/api/v1/ingest/measurements", batch, device, "measurements", ct);
        }

        if (device.Parameters.Count > 0)
        {
            var batch = parameterGenerator.BuildBatch(device, utcNow);
            await SafePostAsync("/api/v1/ingest/process-parameters", batch, device, "process-parameters", ct);
        }
    }

    private async Task SafePostAsync<T>(
        string path, T payload, DeviceProfile device, string kind, CancellationToken ct)
    {
        try
        {
            using var response = await ingestClient.PostAsync(path, payload, device.Serial, device.IngestionSecret, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning(
                    "{Serial} {Kind} → {Status} {Reason}: {Body}",
                    device.Serial, kind, (int)response.StatusCode, response.ReasonPhrase,
                    Truncate(body, 200));
            }
            else
            {
                logger.LogInformation(
                    "{Serial} {Kind} → {Status}", device.Serial, kind, (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Serial} {Kind} threw.", device.Serial, kind);
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

public sealed class EmitterOptions
{
    public int? IntervalSeconds { get; set; }
    public bool RunOnce { get; set; }
}
