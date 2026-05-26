using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Api.Hubs;
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
// SignalR's JSON protocol is configured independently of the MVC pipeline's AddJsonOptions;
// without an explicit StringEnumConverter the broadcast payload serializes
// ComplianceEventType / Status enums as integers (e.g. "Event closed: 2"). Mirror the MVC
// setup so the hub speaks the same JSON dialect the SPA expects from REST.
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

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

// Hangfire dashboard is same-origin and doesn't need CORS. Routing CORS through it
// produces a "CORS policy execution failed" log every ~2s when the dashboard polls
// /hangfire/stats, and also blocks the browser from reading the JSON, so the live
// counters never update. Branching CORS off the /hangfire path fixes both.
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/hangfire"),
    branch => branch.UseCors(Api.Modules.SetupModule.CorsPolicyName));

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Hangfire 1.8.20's JsonStats endpoint unconditionally calls Request.ReadFormAsync()
// even for requests without a body (browser GET to /hangfire/stats, dashboard JS POST
// without Content-Type header, etc.). FormFeature throws "Incorrect Content-Type" on
// the empty body. Forcing a form Content-Type on every /hangfire request that arrives
// without one lets the form-reader parse the (possibly empty) body and respond normally.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/hangfire")
        && string.IsNullOrEmpty(ctx.Request.Headers.ContentType))
    {
        ctx.Request.Headers.ContentType = "application/x-www-form-urlencoded";
    }
    await next();
});

app.UseHangfireDashboardWithAuth();
app.ConfigureDetectionRecurringJobs();

app.MapControllers();
app.MapHub<ComplianceEventsHub>("/hubs/compliance");

app.Run();

public partial class Program { } 