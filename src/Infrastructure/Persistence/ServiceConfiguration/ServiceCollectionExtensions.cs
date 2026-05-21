using Application.Common.Interfaces.Auditing;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Ingestion;
using Application.Common.Interfaces.Notifications;
using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Queries;
using Application.Common.Interfaces.Queries.Emissions;
using Application.Common.Interfaces.Queries.Enterprises;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories;
using Application.Common.Interfaces.Repositories.Auditing;
using Application.Common.Interfaces.Repositories.Emissions;
using Application.Common.Interfaces.Repositories.Enterprises;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Interfaces.Repositories.Notifications;
using Application.Common.Interfaces.Queries.Notifications;
using Infrastructure.Auditing;
using Infrastructure.Identity;
using Infrastructure.Persistence.Repositories.Auditing;
using Infrastructure.Persistence.Repositories.Notifications;
using Application.Common.Settings;
using Infrastructure.Compliance;
using Infrastructure.Compliance.Notifications;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Persistence.Repositories.Common;
using Infrastructure.Persistence.Repositories.EmissionSources;
using Infrastructure.Persistence.Repositories.Enterprises;
using Infrastructure.Persistence.Ingestion;
using Infrastructure.Persistence.Interceptors;
using Infrastructure.Persistence.Repositories.Monitoring;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Infrastructure.Persistence.ServiceConfiguration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistenceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<NpgsqlDataSource>(provider =>
        {
            var settings = provider.GetRequiredService<ApplicationSettings>();
            var connectionString = settings.ConnectionStrings?.DefaultConnection;
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableDynamicJson();
            dataSourceBuilder.UseNetTopologySuite();
            return dataSourceBuilder.Build();
        });

        services.AddSingleton<SoftDeleteSaveChangesInterceptor>();
        services.AddSingleton<TenantBackfillInterceptor>();
        services.AddScoped<TenantSaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
        {
            var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
            var softDeleteInterceptor = provider.GetRequiredService<SoftDeleteSaveChangesInterceptor>();
            var tenantBackfillInterceptor = provider.GetRequiredService<TenantBackfillInterceptor>();
            var tenantInterceptor = provider.GetRequiredService<TenantSaveChangesInterceptor>();
            options
                .UseNpgsql(dataSource,
                    builder =>
                    {
                        builder.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                        builder.UseNetTopologySuite();
                    })
                .AddInterceptors(softDeleteInterceptor, tenantBackfillInterceptor, tenantInterceptor)
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        });
        services.AddScoped<ApplicationDbContextInitializer>();
        services.AddRepositories();
        services.AddScoped<IRawMeasurementWriter, RawMeasurementWriter>();
        services.AddScoped<IRawProcessParameterWriter, RawProcessParameterWriter>();

        services.Configure<ComplianceDetectionSettings>(
            configuration.GetSection("ComplianceDetection"));
        services.AddSingleton<DetectionLockProvider>();
        services.AddScoped<MeasurementMaterializationService>();
        services.AddScoped<ComplianceDetectionService>();
        services.AddScoped<ICurrentViolationProbe, CurrentViolationProbe>();
        services.AddScoped<IEmailComplianceNotificationRenderer, EmailComplianceNotificationRenderer>();
        services.AddScoped<IWebhookComplianceNotificationPayloadBuilder, WebhookComplianceNotificationPayloadBuilder>();
        services.AddHttpClient<IWebhookSender, HttpWebhookSender>();
        services.AddScoped<ComplianceNotificationDispatcher>();
        // Phase C — per-tenant detection jobs registered for DI so Hangfire can resolve them.
        // Recurring-job registration + IHostedService retirement happens in Phase D behind a
        // runner toggle; for now both paths can coexist (advisory-lock keys don't collide
        // because hosted services use kind-level keys while Hangfire jobs use per-(kind, tenant)).
        services.AddScoped<Compliance.Hangfire.PerEnterpriseDetectionJob>();
        services.AddScoped<Compliance.Hangfire.DetectionScheduler>();
        services.AddHostedService<FastDetectionHostedService>();
        services.AddHostedService<AnnualLoadHostedService>();
        services.AddHostedService<CalibrationCheckHostedService>();

        return services;
    }
     private static void AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<EnterpriseRepository>();
        services.AddScoped<IEnterpriseRepository>(provider => provider.GetRequiredService<EnterpriseRepository>());
        services.AddScoped<IEnterpriseQueries>(provider => provider.GetRequiredService<EnterpriseRepository>());

        services.AddScoped<IedCategoryRepository>();
        services.AddScoped<IIedCategoryRepository>(provider => provider.GetRequiredService<IedCategoryRepository>());
        services.AddScoped<IIedCategoryQueries>(provider => provider.GetRequiredService<IedCategoryRepository>());

        services.AddScoped<InstallationRepository>();
        services.AddScoped<IInstallationRepository>(provider => provider.GetRequiredService<InstallationRepository>());
        services.AddScoped<IInstallationQueries>(provider => provider.GetRequiredService<InstallationRepository>());

        services.AddScoped<SectorRepository>();
        services.AddScoped<ISectorRepository>(provider => provider.GetRequiredService<SectorRepository>());
        services.AddScoped<ISectorQueries>(provider => provider.GetRequiredService<SectorRepository>());

        services.AddScoped<SiteRepository>();
        services.AddScoped<ISiteRepository>(provider => provider.GetRequiredService<SiteRepository>());
        services.AddScoped<ISiteQueries>(provider => provider.GetRequiredService<SiteRepository>());
        
        services.AddScoped<EmissionSourceRepository>();
        services.AddScoped<IEmissionSourceRepository>(provider => provider.GetRequiredService<EmissionSourceRepository>());
        services.AddScoped<IEmissionSourceQueries>(provider => provider.GetRequiredService<EmissionSourceRepository>());
        
        services.AddScoped<MeasureUnitRepository>();
        services.AddScoped<IMeasureUnitRepository>(provider => provider.GetRequiredService<MeasureUnitRepository>());
        services.AddScoped<IMeasureUnitQueries>(provider => provider.GetRequiredService<MeasureUnitRepository>());
        
        services.AddScoped<PollutantRepository>();
        services.AddScoped<IPollutantQueries>(provider => provider.GetRequiredService<PollutantRepository>());
        services.AddScoped<IPollutantRepository>(provider => provider.GetRequiredService<PollutantRepository>());
        
        services.AddScoped<MeasurementRepository>();
        services.AddScoped<IMeasurementRepository>(provider => provider.GetRequiredService<MeasurementRepository>());
        services.AddScoped<IMeasurementQueries>(provider => provider.GetRequiredService<MeasurementRepository>());

        services.AddScoped<IRawMeasurementQueries, RawMeasurementQueries>();
        services.AddScoped<IRawProcessParameterQueries, RawProcessParameterQueries>();
        services.AddScoped<IComplianceDetectionQueries, ComplianceDetectionQueries>();
        
        services.AddScoped<MonitoringDeviceRepository>();
        services.AddScoped<IMonitoringDeviceRepository>(provider => provider.GetRequiredService<MonitoringDeviceRepository>());
        services.AddScoped<IMonitoringDeviceQueries>(provider => provider.GetRequiredService<MonitoringDeviceRepository>());
        
        services.AddScoped<ComplianceEventRepository>();
        services.AddScoped<IComplianceEventRepository>(provider => provider.GetRequiredService<ComplianceEventRepository>());
        services.AddScoped<IComplianceEventQueries>(provider => provider.GetRequiredService<ComplianceEventRepository>());

        services.AddScoped<DevicePollutantCapabilityRepository>();
        services.AddScoped<IDevicePollutantCapabilityRepository>(provider => provider.GetRequiredService<DevicePollutantCapabilityRepository>());
        services.AddScoped<IDevicePollutantCapabilityQueries>(provider => provider.GetRequiredService<DevicePollutantCapabilityRepository>());

        services.AddScoped<CalibrationRecordRepository>();
        services.AddScoped<ICalibrationRecordRepository>(provider => provider.GetRequiredService<CalibrationRecordRepository>());
        services.AddScoped<ICalibrationRecordQueries>(provider => provider.GetRequiredService<CalibrationRecordRepository>());
        
        services.AddScoped<PermitRepository>();
        services.AddScoped<IPermitQueries>(provider => provider.GetRequiredService<PermitRepository>());
        services.AddScoped<IPermitRepository>(provider => provider.GetRequiredService<PermitRepository>());
        
        services.AddScoped<EmissionLimitRepository>();
        services.AddScoped<IEmissionLimitRepository>(provider => provider.GetRequiredService<EmissionLimitRepository>());
        
        services.AddScoped<UserRefreshTokenRepository>();
        services.AddScoped<IUserRefreshTokenRepository>(provider => provider.GetRequiredService<UserRefreshTokenRepository>());
        
        services.AddScoped<InvitationRepository>();
        services.AddScoped<IInvitationRepository>(provider => provider.GetRequiredService<InvitationRepository>());

        services.AddScoped<UserEnterpriseMembershipRepository>();
        services.AddScoped<IUserEnterpriseMembershipRepository>(provider => provider.GetRequiredService<UserEnterpriseMembershipRepository>());
        services.AddScoped<IUserEnterpriseMembershipQueries>(provider => provider.GetRequiredService<UserEnterpriseMembershipRepository>());

        services.AddScoped<NotificationSubscriptionRepository>();
        services.AddScoped<INotificationSubscriptionRepository>(provider => provider.GetRequiredService<NotificationSubscriptionRepository>());
        services.AddScoped<INotificationSubscriptionQueries>(provider => provider.GetRequiredService<NotificationSubscriptionRepository>());

        services.AddScoped<AdminAuditLogRepository>();
        services.AddScoped<IAdminAuditLogRepository>(provider => provider.GetRequiredService<AdminAuditLogRepository>());
        services.AddScoped<IAdminAuditService, AdminAuditService>();

        services.AddScoped<LoginAttemptRepository>();
        services.AddScoped<ILoginAttemptRepository>(provider => provider.GetRequiredService<LoginAttemptRepository>());
        services.AddScoped<ILoginAttemptRecorder, LoginAttemptRecorder>();
    }
     

    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>();
        await initializer.InitialiseAsync();
    }
}