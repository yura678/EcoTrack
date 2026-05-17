using Application.Common.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Compliance;

/// <summary>
/// Independently scheduled AnnualLoad detection. Rolling year/month averages move slowly,
/// so this runs daily by default.
/// </summary>
public class AnnualLoadHostedService : BaseDetectionHostedService
{
    private const long AnnualLoadLockKey = 0x4543_4F41_4E4E_4C44; // "ECOANNLD"
    private readonly ComplianceDetectionSettings _settings;

    public AnnualLoadHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<ComplianceDetectionSettings> options,
        ILogger<AnnualLoadHostedService> logger)
        : base(scopeFactory, logger)
    {
        _settings = options.Value;
    }

    protected override string JobName => "AnnualLoad";
    protected override TimeSpan Interval => TimeSpan.FromHours(Math.Max(1, _settings.AnnualLoadIntervalHours));
    protected override long LockKey => AnnualLoadLockKey;
    protected override bool Enabled => _settings.Enabled;

    protected override async Task ExecuteTickAsync(IServiceScope scope, CancellationToken stoppingToken)
    {
        var detection = scope.ServiceProvider.GetRequiredService<ComplianceDetectionService>();
        await detection.RunAnnualLoadAsync(stoppingToken);
    }
}
