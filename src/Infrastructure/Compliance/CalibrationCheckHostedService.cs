using Application.Common.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Compliance;

/// <summary>
/// Independently scheduled calibration-status check. Calibration records change weekly/monthly
/// and overdue status only crosses once per day; default cadence is 6h.
/// </summary>
public class CalibrationCheckHostedService : BaseDetectionHostedService
{
    private const long CalibrationLockKey = 0x4543_4F43_414C_4942; // "ECOCALIB"
    private readonly ComplianceDetectionSettings _settings;

    public CalibrationCheckHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<ComplianceDetectionSettings> options,
        ILogger<CalibrationCheckHostedService> logger)
        : base(scopeFactory, logger)
    {
        _settings = options.Value;
    }

    protected override string JobName => "CalibrationCheck";
    protected override TimeSpan Interval => TimeSpan.FromHours(Math.Max(1, _settings.CalibrationCheckIntervalHours));
    protected override long LockKey => CalibrationLockKey;
    protected override bool Enabled => _settings.Enabled;

    protected override async Task ExecuteTickAsync(IServiceScope scope, CancellationToken stoppingToken)
    {
        var detection = scope.ServiceProvider.GetRequiredService<ComplianceDetectionService>();
        await detection.RunCalibrationChecksAsync(stoppingToken);
    }
}
