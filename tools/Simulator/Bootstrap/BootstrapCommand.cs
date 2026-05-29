using Microsoft.Extensions.Logging;
using Simulator.Config;
using Simulator.Http;

namespace Simulator.Bootstrap;

public sealed class BootstrapCommand(
    EcoTrackApiClient api,
    ILogger<BootstrapCommand> logger)
{
    private static readonly (string Code, decimal Mean, decimal Stdev, decimal RangeMin, decimal RangeMax)[]
        DefaultPollutants =
        {
            ("CO",   50m, 12m, 0m, 200m),
            ("NO₂",  30m,  7m, 0m, 120m),
            ("SO₂",  80m, 18m, 0m, 320m),
            ("PM10", 40m, 10m, 0m, 160m),
        };

    private static readonly (string Type, string UnitSymbol, decimal Mean, decimal Stdev)[]
        DefaultParameters =
        {
            ("VolumetricFlow",   "m³/h", 1200m, 200m),
            ("O2Content",        "%",       8m, 1.5m),
            ("MoistureContent",  "%",      12m,   2m),
        };

    public async Task RunAsync(
        string apiBaseUrl,
        string email,
        string password,
        int enterpriseCount,
        IReadOnlyCollection<Guid> enterpriseIds,
        int devicesPerInstallation,
        bool includeOfflineDevices,
        string configPath,
        CancellationToken ct)
    {
        logger.LogInformation("Logging in as {Email} against {ApiBaseUrl}.", email, apiBaseUrl);
        await api.LoginByPasswordAsync(email, password, ct);

        var pollutants = await api.GetPollutantsAsync(ct);
        var unitsList = await api.GetUnitsAsync(ct);
        var unitsBySymbol = unitsList
            .Where(u => !string.IsNullOrEmpty(u.Symbol))
            .GroupBy(u => u.Symbol, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        var pollutantSpecs = ResolvePollutants(pollutants);
        var parameterSpecs = ResolveParameters(unitsBySymbol);
        if (parameterSpecs.Count == 0)
        {
            logger.LogWarning(
                "None of the default process-parameter units are seeded ({Symbols}). " +
                "Process-parameter ingestion will be skipped for all devices.",
                string.Join(", ", DefaultParameters.Select(p => p.UnitSymbol).Distinct()));
        }

        var enterprises = await api.GetEnterprisesAsync(page: 1, pageSize: 200, ct);
        var approved = enterprises
            .Where(e => !string.Equals(e.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(e.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
            .ToList();

        List<EnterpriseSummary> selectedEnterprises;
        if (enterpriseIds.Count > 0)
        {
            // Explicit selection by id — provision exactly these (ignores --enterprises count).
            var requested = enterpriseIds.ToHashSet();
            selectedEnterprises = approved.Where(e => requested.Contains(e.Id)).ToList();

            var found = selectedEnterprises.Select(e => e.Id).ToHashSet();
            var missing = requested.Where(id => !found.Contains(id)).ToList();
            if (missing.Count > 0)
            {
                logger.LogWarning(
                    "Requested enterprise id(s) not found among approved enterprises and skipped: {Missing}",
                    string.Join(", ", missing));
            }
        }
        else
        {
            selectedEnterprises = approved.Take(enterpriseCount).ToList();
        }

        if (selectedEnterprises.Count == 0)
        {
            throw new InvalidOperationException(
                enterpriseIds.Count > 0
                    ? "None of the requested enterprise id(s) matched an approved enterprise."
                    : "No approved enterprises returned by the API.");
        }

        var config = new SimulatorConfig
        {
            ApiBaseUrl = apiBaseUrl,
            IntervalSeconds = 10,
            IncludeOfflineDevices = includeOfflineDevices,
        };

        // Process parameters (flow/O₂/moisture) describe a physical stack, i.e. an emission source —
        // so attach them to just ONE device per source (the first provisioned for it). Other devices
        // on the same source emit pollutants only. Emission-source ids are globally unique, so one
        // set across the whole run is enough.
        var sourcesWithParameters = new HashSet<Guid>();

        foreach (var enterprise in selectedEnterprises)
        {
            // switch-enterprise is membership-gated and 403s for a superAdmin (which has no
            // per-tenant membership). A superAdmin already bypasses tenant query filters and
            // write validation, and new rows get their EnterpriseId backfilled from the parent
            // FK chain — so the switch is both unnecessary and impossible here. Only switch when
            // running as a tenant user that actually holds a membership in this enterprise.
            if (api.IsSuperAdmin)
            {
                logger.LogInformation(
                    "SuperAdmin session — provisioning '{Name}' ({Id}) without switch-enterprise.",
                    enterprise.Name, enterprise.Id);
            }
            else
            {
                logger.LogInformation("Switching to enterprise '{Name}' ({Id}).", enterprise.Name, enterprise.Id);
                await api.SwitchEnterpriseAsync(enterprise.Id, ct);
            }

            var sites = await api.GetSitesAsync(enterprise.Id, ct);
            if (sites.Count == 0)
            {
                logger.LogWarning("Enterprise {Id} has no sites; skipping.", enterprise.Id);
                continue;
            }

            // Walk every site and every installation so the whole enterprise is provisioned, not
            // just its first installation. Each skip below advances to the next installation/site
            // instead of abandoning the enterprise.
            foreach (var site in sites)
            {
                var installations = await api.GetInstallationsAsync(site.Id, ct);
                if (installations.Count == 0)
                {
                    logger.LogWarning("Site {Id} has no installations; skipping.", site.Id);
                    continue;
                }

                foreach (var installation in installations)
                {
                    var sources = await api.GetEmissionSourcesAsync(installation.Id, page: 1, pageSize: 20, ct);
                    if (sources.Count == 0)
                    {
                        logger.LogWarning("Installation {Id} has no emission sources; skipping.", installation.Id);
                        continue;
                    }

                    var devices = await api.GetMonitoringDevicesAsync(installation.Id, page: 1, pageSize: 50, ct);
                    var selectedDevices = devices
                        .Where(d => !string.Equals(d.Status, "Decommissioned", StringComparison.OrdinalIgnoreCase))
                        .Where(d => includeOfflineDevices
                            || string.Equals(d.Status, "Operational", StringComparison.OrdinalIgnoreCase))
                        .Take(devicesPerInstallation)
                        .ToList();

                    if (selectedDevices.Count == 0)
                    {
                        logger.LogWarning(
                            "Installation {Id} has no usable devices (filter: includeOffline={IncludeOffline}); skipping.",
                            installation.Id, includeOfflineDevices);
                        continue;
                    }

                    var sourceCursor = 0;
                    foreach (var device in selectedDevices)
                    {
                        var emissionSourceId = device.EmissionSourceId
                            ?? sources[sourceCursor++ % sources.Count].Id;

                        // First device to claim this source also carries its process parameters.
                        var emitsProcessParameters = sourcesWithParameters.Add(emissionSourceId);

                        logger.LogInformation(
                            "Provisioning device {Serial} ({Id}) → source {SourceId}{ParamRole}.",
                            device.SerialNumber, device.Id, emissionSourceId,
                            emitsProcessParameters ? " [process-parameter sender]" : string.Empty);

                        var rotated = await api.RotateIngestionSecretAsync(device.Id, ct);
                        var existing = await api.GetCapabilitiesAsync(device.Id, ct);
                        var existingPollutants = existing.Select(c => c.PollutantId).ToHashSet();

                        var pollutantProfiles = new List<PollutantProfile>();
                        foreach (var spec in pollutantSpecs)
                        {
                            if (!existingPollutants.Contains(spec.Pollutant.Id))
                            {
                                await api.CreateCapabilityAsync(
                                    deviceId: device.Id,
                                    pollutantId: spec.Pollutant.Id,
                                    rangeMin: spec.RangeMin,
                                    rangeMax: spec.RangeMax,
                                    rangeUnitId: spec.Pollutant.CanonicalUnitId,
                                    expectedIntervalMinutes: 1,
                                    ct);
                            }

                            pollutantProfiles.Add(new PollutantProfile
                            {
                                PollutantId = spec.Pollutant.Id,
                                Code = spec.Pollutant.Code,
                                UnitId = spec.Pollutant.CanonicalUnitId,
                                Mean = spec.Mean,
                                Stdev = spec.Stdev,
                                RangeMin = spec.RangeMin,
                                RangeMax = spec.RangeMax,
                            });
                        }

                        config.Devices.Add(new DeviceProfile
                        {
                            Serial = device.SerialNumber,
                            IngestionSecret = rotated.Secret,
                            EnterpriseId = enterprise.Id,
                            EmissionSourceId = emissionSourceId,
                            Pollutants = pollutantProfiles,
                            Parameters = emitsProcessParameters
                                ? parameterSpecs.Select(p => new ParameterProfile
                                {
                                    Type = p.Type,
                                    UnitId = p.UnitId,
                                    Mean = p.Mean,
                                    Stdev = p.Stdev,
                                }).ToList()
                                : new List<ParameterProfile>(),
                        });
                    }
                }
            }
        }

        if (config.Devices.Count == 0)
        {
            // Every enterprise was skipped (no sites/installations/sources, or no usable devices).
            // Treat as a no-op rather than a crash, and leave any existing config untouched so a
            // good config isn't clobbered by an empty run.
            logger.LogWarning(
                "Bootstrap produced no devices across {EnterpriseCount} enterprise(s); nothing to write. " +
                "If devices exist but are Offline/Maintenance, re-run with --include-offline true.",
                selectedEnterprises.Count);
            return;
        }

        await ConfigStore.SaveAtomicAsync(configPath, config, ct);
        logger.LogInformation(
            "Provisioned {DeviceCount} devices across {EnterpriseCount} enterprises. Wrote {Path}.",
            config.Devices.Count, selectedEnterprises.Count, configPath);
    }

    private static List<(PollutantSummary Pollutant, decimal Mean, decimal Stdev, decimal RangeMin, decimal RangeMax)>
        ResolvePollutants(IReadOnlyList<PollutantSummary> pollutants)
    {
        var byCode = pollutants
            .Where(p => !string.IsNullOrEmpty(p.Code))
            .GroupBy(p => p.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var resolved = new List<(PollutantSummary, decimal, decimal, decimal, decimal)>();
        foreach (var (code, mean, stdev, rangeMin, rangeMax) in DefaultPollutants)
        {
            if (byCode.TryGetValue(code, out var p))
            {
                resolved.Add((p, mean, stdev, rangeMin, rangeMax));
            }
        }
        return resolved;
    }

    private static List<(string Type, Guid UnitId, decimal Mean, decimal Stdev)>
        ResolveParameters(IReadOnlyDictionary<string, Guid> unitsBySymbol)
    {
        var resolved = new List<(string, Guid, decimal, decimal)>();
        foreach (var (type, symbol, mean, stdev) in DefaultParameters)
        {
            if (unitsBySymbol.TryGetValue(symbol, out var unitId))
            {
                resolved.Add((type, unitId, mean, stdev));
            }
        }
        return resolved;
    }
}
