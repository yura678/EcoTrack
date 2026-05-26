using System.Text.Json;
using Simulator.Json;

namespace Simulator.Config;

public static class ConfigStore
{
    public static async Task<SimulatorConfig> LoadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Simulator config not found at '{path}'. Run `dotnet run --project tools/Simulator -- bootstrap` first.",
                path);
        }

        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<SimulatorConfig>(
            stream, SimulatorJson.ConfigFileOptions, ct);
        if (config is null)
        {
            throw new InvalidDataException($"Simulator config at '{path}' is empty or invalid JSON.");
        }
        return config;
    }

    public static async Task SaveAtomicAsync(string path, SimulatorConfig config, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, config, SimulatorJson.ConfigFileOptions, ct);
            await stream.FlushAsync(ct);
        }

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }
}
