using Application.Common.Settings;
using FluentAssertions;
using Infrastructure.Compliance;
using Infrastructure.Persistence.ServiceConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Tests.Integration.Compliance;

/// <summary>
/// Phase D guardrail: <see cref="ComplianceDetectionSettings.Runner"/> picks one of two paths.
/// HostedService keeps the legacy three IHostedService classes alive; Hangfire skips them and
/// expects the recurring scheduler to drive the cadence instead. A misconfigured toggle that
/// registered both would cause double-execution, so we lock down the conditional with a direct
/// service-collection inspection — no host build needed.
/// </summary>
public class DetectionRunnerToggleTests
{
    [Fact]
    public void HostedServiceRunnerShouldRegisterThreeDetectionHostedServices()
    {
        var services = new ServiceCollection();
        services.AddDetectionRunnerHostedServices(DetectionRunner.HostedService);

        var hostedDescriptors = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType?.Name)
            .ToList();

        hostedDescriptors.Should().Contain(nameof(FastDetectionHostedService));
        hostedDescriptors.Should().Contain(nameof(AnnualLoadHostedService));
        hostedDescriptors.Should().Contain(nameof(CalibrationCheckHostedService));
    }

    [Fact]
    public void HangfireRunnerShouldOmitDetectionHostedServices()
    {
        var services = new ServiceCollection();
        services.AddDetectionRunnerHostedServices(DetectionRunner.Hangfire);

        var hostedDescriptors = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();

        hostedDescriptors.Should().BeEmpty(
            "Hangfire mode hands cadence to the recurring scheduler; no IHostedService should be added");
    }

    [Fact]
    public void DefaultSettingsShouldUseHostedServiceRunner()
    {
        new ComplianceDetectionSettings().Runner.Should().Be(DetectionRunner.HostedService,
            "Hangfire path is opt-in until it has soaked in production");
    }
}
