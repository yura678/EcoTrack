using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simulator.Bootstrap;
using Simulator.Config;
using Simulator.Generation;
using Simulator.Http;
using Simulator.Worker;

const string DefaultApiBaseUrl = "http://localhost:5269";
const string DefaultConfigFile = "simulator.config.json";
const string DefaultSuperAdminEmail = "superAdmin@site.com";
const string DefaultSuperAdminPassword = "qw123321";

var (subcommand, parsedArgs) = ParseArgs(args);

switch (subcommand)
{
    case "bootstrap":
        await RunBootstrapAsync(parsedArgs);
        return 0;

    case "run":
    case null:
        await RunEmitterAsync(parsedArgs);
        return 0;

    case "help":
    case "--help":
    case "-h":
        PrintUsage();
        return 0;

    default:
        Console.Error.WriteLine($"Unknown subcommand: {subcommand}");
        PrintUsage();
        return 2;
}

static (string? subcommand, Dictionary<string, string> args) ParseArgs(string[] argv)
{
    var positional = argv.Length > 0 && !argv[0].StartsWith("--", StringComparison.Ordinal)
        ? argv[0].ToLowerInvariant()
        : null;
    var start = positional is null ? 0 : 1;

    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = start; i < argv.Length; i++)
    {
        var token = argv[i];
        if (!token.StartsWith("--", StringComparison.Ordinal)) continue;

        var key = token[2..];
        if (i + 1 < argv.Length && !argv[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            dict[key] = argv[i + 1];
            i++;
        }
        else
        {
            dict[key] = "true";
        }
    }
    return (positional, dict);
}

static async Task RunBootstrapAsync(Dictionary<string, string> parsedArgs)
{
    var apiBaseUrl = parsedArgs.GetValueOrDefault("api-base") ?? DefaultApiBaseUrl;
    var email = parsedArgs.GetValueOrDefault("email") ?? DefaultSuperAdminEmail;
    var password = parsedArgs.GetValueOrDefault("password") ?? DefaultSuperAdminPassword;
    var configPath = parsedArgs.GetValueOrDefault("config") ?? DefaultConfigFile;
    var enterpriseCount = int.TryParse(parsedArgs.GetValueOrDefault("enterprises"), out var ec) ? ec : 2;
    var devicesPerInstallation = int.TryParse(parsedArgs.GetValueOrDefault("devices"), out var dc) ? dc : 2;
    var includeOffline = string.Equals(
        parsedArgs.GetValueOrDefault("include-offline"), "true", StringComparison.OrdinalIgnoreCase);

    using var host = Host.CreateDefaultBuilder()
        .ConfigureLogging(lb => lb.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }))
        .ConfigureServices((_, services) =>
        {
            services.AddHttpClient<EcoTrackApiClient>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddTransient<BootstrapCommand>();
        })
        .Build();

    using var scope = host.Services.CreateScope();
    var bootstrap = scope.ServiceProvider.GetRequiredService<BootstrapCommand>();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    await bootstrap.RunAsync(
        apiBaseUrl: apiBaseUrl,
        email: email,
        password: password,
        enterpriseCount: enterpriseCount,
        devicesPerInstallation: devicesPerInstallation,
        includeOfflineDevices: includeOffline,
        configPath: configPath,
        ct: cts.Token);
}

static async Task RunEmitterAsync(Dictionary<string, string> parsedArgs)
{
    var configPath = parsedArgs.GetValueOrDefault("config") ?? DefaultConfigFile;
    int? intervalOverride = int.TryParse(parsedArgs.GetValueOrDefault("interval"), out var i) ? i : null;
    var runOnce = string.Equals(parsedArgs.GetValueOrDefault("once"), "true", StringComparison.OrdinalIgnoreCase);

    using var loadCts = new CancellationTokenSource();
    var config = await ConfigStore.LoadAsync(configPath, loadCts.Token);
    var options = new EmitterOptions { IntervalSeconds = intervalOverride, RunOnce = runOnce };

    using var host = Host.CreateDefaultBuilder()
        .ConfigureLogging(lb => lb.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }))
        .ConfigureServices((_, services) =>
        {
            services.AddSingleton(config);
            services.AddSingleton(options);
            services.AddSingleton<GaussianRandom>(_ => new GaussianRandom());
            services.AddSingleton<MeasurementGenerator>();
            services.AddSingleton<ProcessParameterGenerator>();

            services.AddHttpClient<HmacIngestClient>(client =>
            {
                client.BaseAddress = new Uri(config.ApiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            services.AddHostedService<EmitterWorker>();
        })
        .Build();

    await host.RunAsync();
}

static void PrintUsage()
{
    Console.WriteLine("""
        EcoTrack Simulator

        Usage:
          dotnet run --project tools/Simulator -- bootstrap [options]
          dotnet run --project tools/Simulator [-- run] [options]

        Subcommands:
          bootstrap   Login as a SuperAdmin, provision IngestionSecret + DevicePollutantCapability
                      for seeded MonitoringDevices, and write simulator.config.json.
          run         (default) Continuously emit measurement and process-parameter batches
                      using devices declared in the config file.

        Common options:
          --config <path>          Config file (default: simulator.config.json)
          --api-base <url>         API base URL (default: http://localhost:5269)

        Bootstrap options:
          --email <addr>           SuperAdmin email (default: superAdmin@site.com)
          --password <pw>          SuperAdmin password (default: qw123321)
          --enterprises <n>        Number of enterprises to provision (default: 2)
          --devices <n>            Devices per installation (default: 2)
          --include-offline true   Include Offline devices (default: skip)

        Run options:
          --interval <seconds>     Override config.intervalSeconds
          --once true              Emit a single batch and exit
        """);
}
