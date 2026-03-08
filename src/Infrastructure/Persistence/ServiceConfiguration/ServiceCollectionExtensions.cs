using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Queries.Emissions;
using Application.Common.Interfaces.Queries.Enterprises;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories;
using Application.Common.Interfaces.Repositories.Emissions;
using Application.Common.Interfaces.Repositories.Enterprises;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Settings;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Persistence.Repositories.Common;
using Infrastructure.Persistence.Repositories.EmissionSources;
using Infrastructure.Persistence.Repositories.Enterprises;
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

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
        {
            var settings = provider.GetRequiredService<ApplicationSettings>();
            var connectionString = settings.ConnectionStrings?.DefaultConnection;
            var dataSource = new NpgsqlDataSourceBuilder(connectionString)
                .EnableDynamicJson()
                .Build();

            options
                .UseNpgsql(dataSource,
                    builder => { builder.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName); })
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        });
        services.AddScoped<ApplicationDbContextInitializer>();
        // services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddRepositories();

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
        
        services.AddScoped<MonitoringDeviceRepository>();
        services.AddScoped<IMonitoringDeviceRepository>(provider => provider.GetRequiredService<MonitoringDeviceRepository>());
        services.AddScoped<IMonitoringDeviceQueries>(provider => provider.GetRequiredService<MonitoringDeviceRepository>());
        
        services.AddScoped<MonitoringRequirementRepository>();
        services.AddScoped<IMonitoringRequirementRepository>(provider => provider.GetRequiredService<MonitoringRequirementRepository>());
        
        services.AddScoped<ExceedanceEventRepository>();
        services.AddScoped<IExceedanceEventRepository>(provider => provider.GetRequiredService<ExceedanceEventRepository>());
        services.AddScoped<IExceedanceEventQueries>(provider => provider.GetRequiredService<ExceedanceEventRepository>());
        
        services.AddScoped<MonitoringPlanRepository>();
        services.AddScoped<IMonitoringPlanQueries>(provider => provider.GetRequiredService<MonitoringPlanRepository>());
        services.AddScoped<IMonitoringPlanRepository>(provider => provider.GetRequiredService<MonitoringPlanRepository>());
        
        services.AddScoped<PermitRepository>();
        services.AddScoped<IPermitQueries>(provider => provider.GetRequiredService<PermitRepository>());
        services.AddScoped<IPermitRepository>(provider => provider.GetRequiredService<PermitRepository>());
        
        services.AddScoped<EmissionLimitRepository>();
        services.AddScoped<IEmissionLimitRepository>(provider => provider.GetRequiredService<EmissionLimitRepository>());
        
        services.AddScoped<UserRefreshTokenRepository>();
        services.AddScoped<IUserRefreshTokenRepository>(provider => provider.GetRequiredService<UserRefreshTokenRepository>());
        
        services.AddScoped<InvitationRepository>();
        services.AddScoped<IInvitationRepository>(provider => provider.GetRequiredService<InvitationRepository>());
    }
     

    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>();
        await initializer.InitialiseAsync();
    }
}