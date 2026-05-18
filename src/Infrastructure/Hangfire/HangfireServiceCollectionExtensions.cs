using Application.Common.Settings;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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
}
