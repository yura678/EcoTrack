using Application.Common.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Compliance;

/// <summary>
/// Materialises Measurement records from CA and runs the 4 fast detectors
/// (LimitExceedance, DeviceOffline, DataAvailabilityLoss, MissingMeasurement) on each tick.
/// Materialization MUST precede fast detection — they share this service for that reason.
/// </summary>
public class FastDetectionHostedService : BaseDetectionHostedService
{
    private const long FastLockKey = 0x4543_4F44_4554_4543; // "ECODETEC"
    private readonly ComplianceDetectionSettings _settings;

    public FastDetectionHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<ComplianceDetectionSettings> options,
        ILogger<FastDetectionHostedService> logger)
        : base(scopeFactory, logger)
    {
        _settings = options.Value;
    }

    protected override string JobName => "FastDetection";
    protected override TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(1, _settings.ScanIntervalMinutes));
    protected override long LockKey => FastLockKey;
    protected override bool Enabled => _settings.Enabled;

    protected override async Task ExecuteTickAsync(IServiceScope scope, CancellationToken stoppingToken)
    {
        var materialization = scope.ServiceProvider
            .GetRequiredService<MeasurementMaterializationService>();
        await materialization.RunAsync(stoppingToken);

        var detection = scope.ServiceProvider
            .GetRequiredService<ComplianceDetectionService>();
        await detection.RunAsync(stoppingToken);
    }
}
