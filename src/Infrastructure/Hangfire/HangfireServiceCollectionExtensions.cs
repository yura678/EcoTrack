using Application.Common.Settings;
using Hangfire;
using Hangfire.PostgreSql;
using Infrastructure.Compliance.Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Hangfire;

public static class HangfireServiceCollectionExtensions
{
    /// <summary>
    /// Registers Hangfire with PostgreSQL storage. Hangfire creates its own schema on first
    /// startup (PrepareSchemaIfNecessary=true by default), so no EF migration is needed.
    /// </summary>
    public static IServiceCollection AddHangfireServices(this IServiceCollection services)
    {
        services.AddHangfire((provider, config) =>
        {
            var settings = provider.GetRequiredService<ApplicationSettings>();
            var connectionString = settings.ConnectionStrings?.DefaultConnection
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection is required for Hangfire.");

            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(connectionString));
        });

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Math.Max(1, Environment.ProcessorCount);
            // Distinct server name lets multiple instances appear in the dashboard without
            // colliding on heartbeat rows.
            options.ServerName = $"ecotrack-{Environment.MachineName}-{Guid.NewGuid():N}".Substring(0, 50);
        });

        return services;
    }

    public static IApplicationBuilder UseHangfireDashboardWithAuth(this IApplicationBuilder app)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new SuperAdminDashboardAuthorizationFilter()]
        });
        return app;
    }

    /// <summary>
    /// Registers the three master recurring jobs (fast / annual / calibration) when
    /// <see cref="DetectionRunner.Hangfire"/> is the active runner. No-op for HostedService
    /// — the legacy <c>FastDetectionHostedService</c> etc. still drive the cadence in that mode.
    /// Cadences come from <see cref="ComplianceDetectionSettings"/>, so a config edit
    /// re-tunes the schedule on the next deploy without any code change.
    /// </summary>
    public static IApplicationBuilder ConfigureDetectionRecurringJobs(this IApplicationBuilder app)
    {
        var settings = app.ApplicationServices
            .GetRequiredService<IOptions<ComplianceDetectionSettings>>().Value;
        if (settings.Runner != DetectionRunner.Hangfire) return app;

        RecurringJob.AddOrUpdate<DetectionScheduler>(
            recurringJobId: "detection-fast",
            methodCall: s => s.ScheduleFastAsync(CancellationToken.None),
            cronExpression: CronForMinuteInterval(settings.ScanIntervalMinutes));

        RecurringJob.AddOrUpdate<DetectionScheduler>(
            recurringJobId: "detection-calibration",
            methodCall: s => s.ScheduleCalibrationAsync(CancellationToken.None),
            cronExpression: CronForHourInterval(settings.CalibrationCheckIntervalHours));

        RecurringJob.AddOrUpdate<DetectionScheduler>(
            recurringJobId: "detection-annual",
            methodCall: s => s.ScheduleAnnualLoadAsync(CancellationToken.None),
            cronExpression: CronForHourInterval(settings.AnnualLoadIntervalHours));

        return app;
    }

    // Hangfire's MinuteInterval/HourInterval validate the increment against the cron-field range:
    // minutes must be 1..59, hours must be 1..23. Anything ≥ the range size needs a different
    // helper (Cron.Hourly / Cron.Daily) — these clamps prevent a config bump from blowing up
    // app startup.
    private static string CronForMinuteInterval(int minutes) =>
        minutes >= 60 ? Cron.Hourly() : Cron.MinuteInterval(Math.Max(1, minutes));

    private static string CronForHourInterval(int hours) =>
        hours >= 24 ? Cron.Daily() : Cron.HourInterval(Math.Max(1, hours));
}
