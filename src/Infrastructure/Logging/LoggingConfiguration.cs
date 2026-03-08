using System.Data;
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
        #region Enriching Logger Context

        var env = context.HostingEnvironment;


        configuration.Enrich.FromLogContext()
            .Enrich.WithProperty("ApplicationName", env.ApplicationName)
            .Enrich.WithProperty("Environment", env.EnvironmentName)
            .Enrich.WithSpan()
            .Enrich.WithExceptionDetails();

        #endregion


        if (!context.HostingEnvironment.IsDevelopment())
        {
            //
        }
        else
        {
            configuration.WriteTo.Console().MinimumLevel.Information();
            configuration.WriteTo.File(new JsonFormatter(), "logs/log.json").MinimumLevel.Information();
        }
    };
}