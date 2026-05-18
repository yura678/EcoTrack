using System.Diagnostics;
using System.Threading.RateLimiting;
using Api.Modules;
using Api.Swagger;
using Application.ServiceConfiguration;
using Infrastructure.EmailProvider.ServiceConfiguration;
using Infrastructure.Hangfire;
using Infrastructure.Identity.Dtos;
using Infrastructure.Identity.ServiceConfiguration;
using Infrastructure.Logging;
using Infrastructure.Persistence.ServiceConfiguration;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog(LoggingConfiguration.ConfigureLogger);

var configuration = builder.Configuration;
Activity.DefaultIdFormat = ActivityIdFormat.W3C;


builder.Services.Configure<IdentitySettings>(configuration.GetSection(nameof(IdentitySettings)));
var identitySettings = configuration.GetSection(nameof(IdentitySettings)).Get<IdentitySettings>();

builder.Services.SetupServices(configuration)
    .AddApplicationServices()
    .RegisterIdentityServices(identitySettings)
    .AddEmailProviderServices(configuration)
    .AddPersistenceServices(configuration)
    .AddHangfireServices();

builder.Services.RegisterValidatorsAsServices();
builder.Services.AddMemoryCache();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
    {
        var partition = httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? httpContext.Request.Headers["X-Forwarded-For"].ToString()
                        ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});


builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwagger("v1","v1.1");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerAndUi();
    app.UseRouting();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
}

await app.InitialiseDatabaseAsync();
await app.SeedDefaultUsersAsync();

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseCors(Api.Modules.SetupModule.CorsPolicyName);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboardWithAuth();

app.MapControllers();

app.Run();

public partial class Program { } 