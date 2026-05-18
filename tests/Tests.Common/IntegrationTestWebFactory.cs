using Application.Common.Settings;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Tests.Common;

public class IntegrationTestWebFactory: WebApplicationFactory<Program>, IAsyncLifetime
{
  private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("timescale/timescaledb-ha:pg16")
        .WithDatabase("test-ecoTrack-database")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // ApplicationSettings is consumed by Hangfire to get its connection string.
            // Without this override Hangfire's JobStorage tries to connect to the
            // production-config DB at app startup and tests fail before any code runs.
            services.RemoveServiceByType(typeof(ApplicationSettings));
            services.AddSingleton(new ApplicationSettings
            {
                ConnectionStrings = new ConectionStrings
                {
                    DefaultConnection = _dbContainer.GetConnectionString()
                }
            });

            RegisterDatabase(services);
            DisableHangfire(services);

            services.AddAuthentication(defaultScheme: "TestScheme")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    "TestScheme", _ => { });

            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
                options.DefaultScheme = "TestScheme";
            });
        }).ConfigureAppConfiguration((_, config) =>
        {
            config
                .AddJsonFile("appsettings.Test.json")
                .AddEnvironmentVariables();
        });
    }

    /// <summary>
    /// Tests don't need real background-job processing but the dashboard middleware in
    /// Program.cs requires AddHangfire to have been called. Strip the Hangfire processing
    /// server (the IHostedService that polls JobStorage for work) so worker threads don't
    /// churn against the testcontainer, but keep storage + client registrations intact so
    /// the rest of the wiring resolves.
    /// </summary>
    private static void DisableHangfire(IServiceCollection services)
    {
        var serverDescriptors = services
            .Where(d => d.ImplementationType?.FullName?.Contains("BackgroundJobServerHostedService") == true
                        || d.ImplementationType?.FullName?.Contains("BackgroundProcessing") == true)
            .ToList();
        foreach (var d in serverDescriptors) services.Remove(d);

        services.RemoveServiceByType(typeof(IBackgroundJobClient));
        services.AddSingleton<IBackgroundJobClient, NoopBackgroundJobClient>();
    }

    private void RegisterDatabase(IServiceCollection services)
    {
        services.RemoveServiceByType(typeof(DbContextOptions<ApplicationDbContext>));
        // Replace the singleton NpgsqlDataSource too — the raw ingest writers connect through
        // it directly (COPY), so without this they would still hit the production-config DB.
        services.RemoveServiceByType(typeof(NpgsqlDataSource));

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_dbContainer.GetConnectionString());
        dataSourceBuilder.EnableDynamicJson();
        dataSourceBuilder.UseNetTopologySuite();
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);

        services.AddDbContext<ApplicationDbContext>(options => options
            .UseNpgsql(
                dataSource,
                builder =>
                {
                    builder.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    builder.UseNetTopologySuite();
                })
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
    }

    public Task InitializeAsync()
    {
        return _dbContainer.StartAsync();
    }

    public new Task DisposeAsync()
    {
        return _dbContainer.DisposeAsync().AsTask();
    }
}

public static class TestFactoryExtensions
{
    public static void RemoveServiceByType(this IServiceCollection services, Type serviceType)
    {
        var descriptor = services.SingleOrDefault(s => s.ServiceType == serviceType);
        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }
    }
}

internal class NoopBackgroundJobClient : IBackgroundJobClient
{
    public string Create(Job job, IState state) => Guid.NewGuid().ToString();
    public bool ChangeState(string jobId, IState state, string? expectedState) => true;
}
