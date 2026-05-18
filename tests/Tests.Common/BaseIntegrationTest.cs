using System.Net.Http.Headers;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Tests.Common;

public abstract class BaseIntegrationTest : IClassFixture<IntegrationTestWebFactory>, IDisposable
{
    protected readonly ApplicationDbContext Context;
    protected readonly HttpClient Client;
    private readonly IServiceScope _scope;

    protected BaseIntegrationTest(IntegrationTestWebFactory factory)
    {
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "TestScheme");

        _scope = factory.Services.CreateScope();
        Context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    protected async Task SaveChangesAsync()
    {
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    /// <summary>
    /// Wipes test-domain data ignoring soft-delete filters. Use in test class DisposeAsync.
    /// Order respects FK dependencies (children → parents).
    /// </summary>
    protected async Task ResetTenantDataAsync()
    {
        await Context.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE
                "raw_measurement",
                "raw_process_parameter",
                "calibration_record",
                "device_pollutant_capability",
                "notification_subscription",
                "compliance_event",
                "measurement",
                "emission_limit",
                "permit",
                "monitoring_device",
                "water_emission_source",
                "air_emission_source",
                "emission_source",
                "installation",
                "site",
                "enterprise",
                "ied_category",
                "pollutant",
                "measure_unit",
                "sector"
            RESTART IDENTITY CASCADE;
        """);
        Context.ChangeTracker.Clear();
    }

    public void Dispose() => _scope.Dispose();
}
