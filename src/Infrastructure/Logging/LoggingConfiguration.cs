using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Formatting.Json;

namespace Infrastructure.Logging;

public static class LoggingConfiguration
{
    public static Action<HostBuilderContext, LoggerConfiguration> ConfigureLogger => (context, configuration) =>
    {
        var env = context.HostingEnvironment;

        configuration.Enrich.FromLogContext()
            .Enrich.WithProperty("ApplicationName", env.ApplicationName)
            .Enrich.WithProperty("Environment", env.EnvironmentName)
            .Enrich.WithSpan()
            .Enrich.WithExceptionDetails();

        if (context.HostingEnvironment.IsDevelopment())
        {
            configuration.WriteTo.Console().MinimumLevel.Information();
            configuration.WriteTo.File(
                formatter: new JsonFormatter(),
                path: "logs/log-.json",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: false).MinimumLevel.Information();
        }
    };
}