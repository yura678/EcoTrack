using System.Diagnostics;
using Api.Modules;
using Api.Swagger;
using Application.ServiceConfiguration;
using Infrastructure.EmailProvider.ServiceConfiguration;
using Infrastructure.Identity.Dtos;
using Infrastructure.Identity.ServiceConfiguration;
using Infrastructure.Logging;
using Infrastructure.Persistence.ServiceConfiguration;
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
    .AddPersistenceServices(configuration);

builder.Services.RegisterValidatorsAsServices();


builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwagger("v1","v1.1");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerAndUi();
    app.UseRouting();
    app.UseDeveloperExceptionPage();
}

await app.InitialiseDatabaseAsync();
await app.SeedDefaultUsersAsync();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { } 