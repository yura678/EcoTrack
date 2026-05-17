using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Settings;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Compliance;

public class ComplianceDetectionService(
    ApplicationDbContext context,
    IComplianceEventRepository complianceEventRepository,
    IComplianceEventQueries complianceEventQueries,
    IUnitOfWork unitOfWork,
    IOptions<ComplianceDetectionSettings> options,
    ILogger<ComplianceDetectionService> logger)
{
    private readonly ComplianceDetectionSettings _settings = options.Value;

    /// <summary>
    /// Fast-cadence detectors. Run every tick (5 min by default).
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var newEvents = new List<ComplianceEvent>();

        newEvents.AddRange(await DetectLimitExceedancesAsync(cancellationToken));
        newEvents.AddRange(await DetectDeviceOfflineAsync(cancellationToken));
        newEvents.AddRange(await DetectCalibrationFailuresAsync(cancellationToken));
        newEvents.AddRange(await DetectDataAvailabilityLossAsync(cancellationToken));
        newEvents.AddRange(await DetectMissingMeasurementAsync(cancellationToken));

        await PersistAsync(newEvents, cancellationToken);

        logger.LogInformation(
            "Compliance detection: {New} new events in {Ms}ms",
            newEvents.Count, (DateTime.UtcNow - start).TotalMilliseconds);
    }

    /// <summary>
    /// Slow-cadence detectors that scan large historical windows.
    /// AnnualLoad averages move slowly; running this every fast tick wastes CPU.
    /// </summary>
    public async Task RunAnnualLoadAsync(CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var newEvents = await DetectAnnualLoadExceedancesAsync(cancellationToken);

        await PersistAsync(newEvents, cancellationToken);

        logger.LogInformation(
            "AnnualLoad detection: {New} new events in {Ms}ms",
            newEvents.Count, (DateTime.UtcNow - start).TotalMilliseconds);
    }

    private async Task PersistAsync(List<ComplianceEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;
        await complianceEventRepository.AddRangeAsync(events, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ─── LimitExceedance ─────────────────────────────────────────────────────────

    private static readonly LimitType[] RateBasedLimits = [LimitType.Concentration, LimitType.MassFlow];

    private async Task<List<ComplianceEvent>> DetectLimitExceedancesAsync(CancellationToken ct)
    {
        var targets = await GetActiveLimitTargetsAsync(RateBasedLimits, ct);
        if (targets.Count == 0) return [];

        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.LimitExceedance, ct);
        var existingKeys = existing
            .Where(e => e.LimitId.HasValue)
            .Select(e => (e.LimitId!.Value, e.EmissionSourceId))
            .ToHashSet();

        var newEvents = new List<ComplianceEvent>();

        foreach (var byPeriod in targets.GroupBy(t => t.Period))
        {
            var (_, to) = ComputeLastCompletedWindow(byPeriod.Key);
            if (to == default) continue;

            var sourceIds = byPeriod.Select(t => t.EmissionSourceId).Distinct().ToArray();
            var pollutantIds = byPeriod.Select(t => t.PollutantId).Distinct().ToArray();

            var measurements = await context.Set<Measurement>()
                .Where(m => sourceIds.Contains(m.EmissionSourceId)
                            && pollutantIds.Contains(m.PollutantId)
                            && m.Window == byPeriod.Key
                            && m.Aggregation == Aggregation.Average
                            && m.WindowEnd == to)
                .Select(m => new
                {
                    m.Id, m.EmissionSourceId, m.PollutantId,
                    m.Value, m.NormalizedValue, m.UnitId, m.Quality, m.WindowStart, m.WindowEnd
                })
                .ToListAsync(ct);

            var byKey = measurements.ToDictionary(m => (m.EmissionSourceId, m.PollutantId));

            var unitIds = byPeriod.Select(t => t.UnitId)
                .Concat(measurements.Select(m => m.UnitId))
                .Distinct()
                .ToArray();
            var units = await LoadUnitsAsync(unitIds, ct);

            // Pre-fetch volumetric flow for the window if any MassFlow limit might need
            // to be derived from a MassConcentration measurement.
            var needsFlow = byPeriod.Any(t =>
                units.TryGetValue(t.UnitId, out var u)
                && u.Dimension == MeasureUnitDimension.MassFlow);
            var flowByKey = needsFlow
                ? await GetVolumetricFlowForWindowAsync(sourceIds, byPeriod.Key, to, ct)
                : new Dictionary<Guid, (decimal Value, Guid UnitId)>();
            if (flowByKey.Count > 0)
            {
                var flowUnitIds = flowByKey.Values.Select(v => v.UnitId).Distinct().ToArray();
                var flowUnits = await LoadUnitsAsync(flowUnitIds, ct);
                foreach (var (uid, info) in flowUnits) units.TryAdd(uid, info);
            }

            foreach (var t in byPeriod)
            {
                if (existingKeys.Contains((t.LimitId, t.EmissionSourceId))) continue;
                if (!byKey.TryGetValue((t.EmissionSourceId, t.PollutantId), out var m)) continue;
                if (m.Quality != Quality.Valid) continue; // untrusted reading — don't raise events
                if (!units.TryGetValue(t.UnitId, out var limitUnit)
                    || !units.TryGetValue(m.UnitId, out var measurementUnit)) continue;

                if (limitUnit.Dimension != measurementUnit.Dimension)
                {
                    var derived = TryDeriveMassFlow(t, m.Value, limitUnit, measurementUnit, flowByKey, units);
                    if (derived is null)
                    {
                        logger.LogWarning(
                            "Limit {LimitId} ({LimitDim}) and measurement {MeasurementId} ({MeasDim}) " +
                            "use incompatible dimensions and no derivation path applies; skipping.",
                            t.LimitId, limitUnit.Dimension, m.Id, measurementUnit.Dimension);
                        continue;
                    }
                    if (derived.MassFlowKgPerH <= derived.LimitKgPerH) continue;

                    var derivedRatio = Math.Round(derived.MassFlowKgPerH / derived.LimitKgPerH, 4);
                    newEvents.Add(ComplianceEvent.ForLimitExceedance(
                        Guid.NewGuid(), t.EmissionSourceId,
                        measurementId: m.Id, t.LimitId, derivedRatio, m.WindowStart, m.WindowEnd,
                        notes: $"Derived mass flow {derived.MassFlowKgPerH:0.###} kg/h " +
                               $"({m.Value:0.###} {measurementUnit.Symbol} × " +
                               $"{derived.FlowDescription}) > " +
                               $"{t.Value:0.###} {limitUnit.Symbol} (ratio {derivedRatio:0.##})"));
                    continue;
                }

                // For Concentration limits, regulator expresses limits at reference conditions
                // (e.g. "200 mg/m³ NOx @ 6% O₂"). Prefer NormalizedValue when available;
                // MassFlow/AnnualLoad rates are unaffected by normalization, so still use Value
                // (but those go through derived-mass-flow path above, never here).
                var effectiveValue = m.NormalizedValue ?? m.Value;
                var measuredBase = effectiveValue * measurementUnit.ToBaseFactor;
                var limitBase = t.Value * limitUnit.ToBaseFactor;
                if (measuredBase <= limitBase) continue;

                var ratio = Math.Round(measuredBase / limitBase, 4);
                var valueLabel = m.NormalizedValue.HasValue
                    ? $"{effectiveValue:0.###} {measurementUnit.Symbol} (normalized)"
                    : $"{m.Value:0.###} {measurementUnit.Symbol}";
                newEvents.Add(ComplianceEvent.ForLimitExceedance(
                    Guid.NewGuid(), t.EmissionSourceId,
                    measurementId: m.Id, t.LimitId, ratio, m.WindowStart, m.WindowEnd,
                    notes: $"{valueLabel} > {t.Value:0.###} {limitUnit.Symbol} (ratio {ratio:0.##})"));
            }
        }

        return newEvents;
    }

    private record UnitInfo(string Symbol, MeasureUnitDimension Dimension, decimal ToBaseFactor);

    private async Task<Dictionary<Guid, UnitInfo>> LoadUnitsAsync(Guid[] unitIds, CancellationToken ct)
    {
        if (unitIds.Length == 0) return [];
        return await context.Set<MeasureUnit>()
            .Where(u => unitIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Symbol, u.Dimension, u.ToBaseFactor })
            .ToDictionaryAsync(u => u.Id,
                u => new UnitInfo(u.Symbol, u.Dimension, u.ToBaseFactor), ct);
    }

    private record DerivedMassFlow(decimal MassFlowKgPerH, decimal LimitKgPerH, string FlowDescription);

    /// <summary>
    /// Computes mass flow on the fly from a concentration measurement + volumetric flow process
    /// parameter. Supports the common CEMS setup where the device only reports concentration
    /// (mg/m³) and a separate Pitot/anemometer provides flow (m³/h).
    /// Returns null if derivation is impossible (unsupported dim combo, no flow data,
    /// or flow unit dimension mismatch).
    /// </summary>
    private static DerivedMassFlow? TryDeriveMassFlow(
        LimitTarget t, decimal measurementValue,
        UnitInfo limitUnit, UnitInfo measurementUnit,
        IReadOnlyDictionary<Guid, (decimal Value, Guid UnitId)> flowByKey,
        IReadOnlyDictionary<Guid, UnitInfo> units)
    {
        if (limitUnit.Dimension != MeasureUnitDimension.MassFlow) return null;
        if (measurementUnit.Dimension != MeasureUnitDimension.MassConcentration) return null;
        if (!flowByKey.TryGetValue(t.EmissionSourceId, out var flow)) return null;
        if (!units.TryGetValue(flow.UnitId, out var flowUnit)) return null;
        if (flowUnit.Dimension != MeasureUnitDimension.VolumetricFlow) return null;

        // measurement (mg/m³ base) × flow (m³/h base) = mg/h → /1e6 = kg/h
        var concBase = measurementValue * measurementUnit.ToBaseFactor;
        var flowBase = flow.Value * flowUnit.ToBaseFactor;
        var massFlowKgPerH = (concBase * flowBase) / 1_000_000m;
        var limitKgPerH = t.Value * limitUnit.ToBaseFactor;

        return new DerivedMassFlow(
            MassFlowKgPerH: Math.Round(massFlowKgPerH, 6),
            LimitKgPerH: limitKgPerH,
            FlowDescription: $"{flow.Value:0.###} {flowUnit.Symbol}");
    }

    private Task<Dictionary<Guid, (decimal Value, Guid UnitId)>> GetVolumetricFlowForWindowAsync(
        Guid[] sourceIds, AveragingWindow period, DateTime windowEnd, CancellationToken ct)
    {
        var periodSpan = PeriodToTimeSpan(period);
        return GetVolumetricFlowForRangeAsync(sourceIds, windowEnd - periodSpan, windowEnd, ct);
    }

    private async Task<Dictionary<Guid, (decimal Value, Guid UnitId)>> GetVolumetricFlowForRangeAsync(
        Guid[] sourceIds, DateTime from, DateTime to, CancellationToken ct)
    {
        var sql = @"
            SELECT
                emission_source_id,
                (SUM(valid_sum) / NULLIF(SUM(valid_count), 0))::numeric(18,6) AS avg_flow,
                (array_agg(unit_id))[1] AS unit_id
            FROM process_parameter_1m
            WHERE emission_source_id = ANY(@source_ids)
              AND parameter_type = 4  -- ParameterType.VolumetricFlow
              AND bucket >= @from
              AND bucket < @to
            GROUP BY emission_source_id
            HAVING SUM(valid_count) > 0";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, sourceIds);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));

        var dict = new Dictionary<Guid, (decimal, Guid)>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var sourceId = reader.GetGuid(0);
            var value = reader.GetDecimal(1);
            var unitId = reader.GetGuid(2);
            dict[sourceId] = (value, unitId);
        }
        return dict;
    }

    // ─── AnnualLoad (rolling rate average against rate limit) ────────────────────

    private async Task<List<ComplianceEvent>> DetectAnnualLoadExceedancesAsync(CancellationToken ct)
    {
        var targets = await GetActiveLimitTargetsAsync([LimitType.AnnualLoad], ct);
        if (targets.Count == 0) return [];

        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.LimitExceedance, ct);
        var existingKeys = existing
            .Where(e => e.LimitId.HasValue)
            .Select(e => (e.LimitId!.Value, e.EmissionSourceId))
            .ToHashSet();

        var newEvents = new List<ComplianceEvent>();
        var now = DateTime.UtcNow;

        foreach (var byPeriod in targets.GroupBy(t => t.Period))
        {
            var window = AnnualLoadPeriodToTimeSpan(byPeriod.Key);
            if (window == TimeSpan.Zero)
            {
                logger.LogWarning(
                    "AnnualLoad limit uses unsupported period {Period}; skipping.", byPeriod.Key);
                continue;
            }

            var from = now - window;
            var sourceIds = byPeriod.Select(t => t.EmissionSourceId).Distinct().ToArray();
            var pollutantIds = byPeriod.Select(t => t.PollutantId).Distinct().ToArray();

            var rolling = await GetRollingAverageRateAsync(sourceIds, pollutantIds, from, now, ct);
            if (rolling.Count == 0) continue;

            var unitIds = byPeriod.Select(t => t.UnitId)
                .Concat(rolling.Values.Select(r => r.UnitId))
                .Distinct()
                .ToArray();
            var units = await LoadUnitsAsync(unitIds, ct);

            // Pre-fetch volumetric flow over the same rolling window so MassFlow limits with
            // concentration measurements can be derived.
            var needsFlow = byPeriod.Any(t =>
                units.TryGetValue(t.UnitId, out var u)
                && u.Dimension == MeasureUnitDimension.MassFlow);
            var flowByKey = needsFlow
                ? await GetVolumetricFlowForRangeAsync(sourceIds, from, now, ct)
                : new Dictionary<Guid, (decimal Value, Guid UnitId)>();
            if (flowByKey.Count > 0)
            {
                var flowUnitIds = flowByKey.Values.Select(v => v.UnitId).Distinct().ToArray();
                var flowUnits = await LoadUnitsAsync(flowUnitIds, ct);
                foreach (var (uid, info) in flowUnits) units.TryAdd(uid, info);
            }

            foreach (var t in byPeriod)
            {
                if (existingKeys.Contains((t.LimitId, t.EmissionSourceId))) continue;
                if (!rolling.TryGetValue((t.EmissionSourceId, t.PollutantId), out var r)) continue;
                if (!units.TryGetValue(t.UnitId, out var limitUnit)
                    || !units.TryGetValue(r.UnitId, out var measurementUnit)) continue;

                if (limitUnit.Dimension != measurementUnit.Dimension)
                {
                    var derived = TryDeriveMassFlow(t, r.AvgRate, limitUnit, measurementUnit, flowByKey, units);
                    if (derived is null)
                    {
                        logger.LogWarning(
                            "AnnualLoad limit {LimitId} ({LimitDim}) and measurement unit {MeasUnit} ({MeasDim}) " +
                            "use incompatible dimensions and no derivation path applies; skipping.",
                            t.LimitId, limitUnit.Dimension, r.UnitId, measurementUnit.Dimension);
                        continue;
                    }
                    if (derived.MassFlowKgPerH <= derived.LimitKgPerH) continue;

                    var derivedRatio = Math.Round(derived.MassFlowKgPerH / derived.LimitKgPerH, 4);
                    newEvents.Add(ComplianceEvent.ForLimitExceedance(
                        Guid.NewGuid(), t.EmissionSourceId,
                        measurementId: null, t.LimitId, derivedRatio, from, now,
                        notes: $"AnnualLoad derived: {derived.MassFlowKgPerH:0.###} kg/h " +
                               $"({r.AvgRate:0.###} {measurementUnit.Symbol} × {derived.FlowDescription}) " +
                               $"over last {window.TotalDays:0}d > {t.Value:0.###} {limitUnit.Symbol} " +
                               $"(ratio {derivedRatio:0.##})"));
                    continue;
                }

                var measuredBase = r.AvgRate * measurementUnit.ToBaseFactor;
                var limitBase = t.Value * limitUnit.ToBaseFactor;
                if (measuredBase <= limitBase) continue;

                var ratio = Math.Round(measuredBase / limitBase, 4);
                newEvents.Add(ComplianceEvent.ForLimitExceedance(
                    Guid.NewGuid(), t.EmissionSourceId,
                    measurementId: null, t.LimitId, ratio, from, now,
                    notes: $"AnnualLoad: avg {r.AvgRate:0.###} {measurementUnit.Symbol} " +
                           $"over last {window.TotalDays:0}d > limit {t.Value:0.###} {limitUnit.Symbol} " +
                           $"(ratio {ratio:0.##}, {r.Samples} samples)"));
            }
        }

        return newEvents;
    }

    private record RollingAverage(decimal AvgRate, long Samples, Guid UnitId);

    private async Task<Dictionary<(Guid SourceId, Guid PollutantId), RollingAverage>>
        GetRollingAverageRateAsync(
            Guid[] sourceIds, Guid[] pollutantIds,
            DateTime from, DateTime to, CancellationToken ct)
    {
        // Reads from measurement_1m (pre-aggregated 1-minute buckets) for ~60× faster annual
        // scans vs raw_measurement. The CA stores valid_sum (Quality=0 only) and valid_count,
        // so we get true valid-only averages without scanning raw data.
        var sql = @"
            SELECT
                m.emission_source_id,
                m.pollutant_id,
                (SUM(m.valid_sum) / NULLIF(SUM(m.valid_count), 0))::numeric(18,6) AS avg_rate,
                COALESCE(SUM(m.valid_count), 0)::bigint AS samples,
                (array_agg(m.unit_id))[1] AS unit_id
            FROM measurement_1m m
            JOIN emission_source es ON es.id = m.emission_source_id
            WHERE m.emission_source_id = ANY(@source_ids)
              AND m.pollutant_id = ANY(@pollutant_ids)
              AND m.bucket >= @from
              AND m.bucket < @to
              AND es.deleted_at IS NULL
            GROUP BY m.emission_source_id, m.pollutant_id
            HAVING SUM(m.valid_count) > 0";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, sourceIds);
        AddParam(command, "pollutant_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, pollutantIds);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));

        var dict = new Dictionary<(Guid, Guid), RollingAverage>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var sourceId = reader.GetGuid(0);
            var pollutantId = reader.GetGuid(1);
            var avg = reader.GetDecimal(2);
            var samples = reader.GetInt64(3);
            var unitId = reader.GetGuid(4);
            dict[(sourceId, pollutantId)] = new RollingAverage(avg, samples, unitId);
        }
        return dict;
    }

    private static TimeSpan AnnualLoadPeriodToTimeSpan(AveragingWindow period) => period switch
    {
        AveragingWindow.Month1 => TimeSpan.FromDays(30),
        AveragingWindow.Year1 => TimeSpan.FromDays(365),
        _ => TimeSpan.Zero
    };

    // ─── DeviceOffline ───────────────────────────────────────────────────────────

    private async Task<List<ComplianceEvent>> DetectDeviceOfflineAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var threshold = TimeSpan.FromMinutes(_settings.DeviceOfflineThresholdMinutes);
        var cutoff = now - threshold;
        var graceLine = now - TimeSpan.FromDays(Math.Max(0, _settings.NewDeviceGraceDays));

        var devices = await context.Set<MonitoringDevice>()
            .Where(d => d.Status == DeviceStatus.Operational && d.EmissionSourceId != null)
            .Select(d => new { d.Id, d.EmissionSourceId, d.InstalledAt })
            .ToListAsync(ct);

        if (devices.Count == 0) return [];

        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.DeviceOffline, ct);
        var existingDeviceIds = existing
            .Where(e => e.DeviceId.HasValue)
            .Select(e => e.DeviceId!.Value)
            .ToHashSet();

        var deviceIds = devices.Select(d => d.Id).ToArray();
        var lastSeen = await GetDeviceLastSeenAsync(deviceIds, ct);

        var newEvents = new List<ComplianceEvent>();
        foreach (var d in devices)
        {
            if (existingDeviceIds.Contains(d.Id)) continue;
            if (d.InstalledAt.HasValue && d.InstalledAt.Value > graceLine) continue; // grace

            var seen = lastSeen.GetValueOrDefault(d.Id);
            if (seen.HasValue && seen.Value >= cutoff) continue;

            newEvents.Add(ComplianceEvent.ForDeviceOffline(
                Guid.NewGuid(), d.EmissionSourceId!.Value, d.Id,
                cutoff, now,
                notes: seen.HasValue
                    ? $"Last seen {seen.Value:O}"
                    : "No measurements ingested yet"));
        }
        return newEvents;
    }

    // ─── CalibrationFailure ──────────────────────────────────────────────────────

    private async Task<List<ComplianceEvent>> DetectCalibrationFailuresAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var graceLine = now - TimeSpan.FromDays(Math.Max(0, _settings.NewDeviceGraceDays));

        // Latest calibration record per device, joined to operational source-bound devices.
        var latest = await context.Set<MonitoringDevice>()
            .Where(d => d.Status == DeviceStatus.Operational && d.EmissionSourceId != null)
            .Select(d => new
            {
                Device = d,
                Last = context.Set<CalibrationRecord>()
                    .Where(c => c.DeviceId == d.Id)
                    .OrderByDescending(c => c.PerformedAt)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.CalibrationFailure, ct);
        var existingDeviceIds = existing
            .Where(e => e.DeviceId.HasValue)
            .Select(e => e.DeviceId!.Value)
            .ToHashSet();

        var newEvents = new List<ComplianceEvent>();
        foreach (var row in latest)
        {
            if (existingDeviceIds.Contains(row.Device.Id)) continue;
            var installedAt = row.Device.InstalledAt;

            if (row.Last is null)
            {
                // No calibration ever recorded — alert only after grace period (gives time to commission).
                if (installedAt is null || installedAt.Value > graceLine) continue;

                newEvents.Add(ComplianceEvent.ForCalibrationFailure(
                    Guid.NewGuid(), row.Device.EmissionSourceId!.Value, row.Device.Id,
                    installedAt.Value, now,
                    notes: $"No calibration record found; device installed {installedAt.Value:O}"));
                continue;
            }

            var failed = row.Last.Result == CalibrationResult.Fail;
            var overdue = row.Last.NextDueAt < now;
            if (!failed && !overdue) continue;

            newEvents.Add(ComplianceEvent.ForCalibrationFailure(
                Guid.NewGuid(), row.Device.EmissionSourceId!.Value, row.Device.Id,
                row.Last.NextDueAt, now,
                notes: failed
                    ? $"Last calibration {row.Last.PerformedAt:O} returned Fail"
                    : $"Calibration overdue since {row.Last.NextDueAt:O}"));
        }
        return newEvents;
    }

    // ─── DataAvailabilityLoss ────────────────────────────────────────────────────

    private async Task<List<ComplianceEvent>> DetectDataAvailabilityLossAsync(CancellationToken ct)
    {
        var targets = await GetActiveLimitTargetsAsync(RateBasedLimits, ct);
        if (targets.Count == 0) return [];

        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.DataAvailabilityLoss, ct);
        var existingSourceIds = existing.Select(e => e.EmissionSourceId).ToHashSet();

        var newEvents = new List<ComplianceEvent>();
        var seenSources = new HashSet<Guid>();

        foreach (var byPeriod in targets.GroupBy(t => t.Period))
        {
            var (_, to) = ComputeLastCompletedWindow(byPeriod.Key);
            if (to == default) continue;

            var sourceIds = byPeriod.Select(t => t.EmissionSourceId).Distinct().ToArray();
            var pollutantIds = byPeriod.Select(t => t.PollutantId).Distinct().ToArray();

            var measurements = await context.Set<Measurement>()
                .Where(m => sourceIds.Contains(m.EmissionSourceId)
                            && pollutantIds.Contains(m.PollutantId)
                            && m.Window == byPeriod.Key
                            && m.Aggregation == Aggregation.Average
                            && m.WindowEnd == to)
                .Select(m => new
                {
                    m.Id, m.EmissionSourceId, m.PollutantId,
                    m.WindowStart, m.WindowEnd, m.ValidPointsCount, m.ExpectedPointsCount
                })
                .ToListAsync(ct);

            var byKey = measurements.ToDictionary(m => (m.EmissionSourceId, m.PollutantId));

            foreach (var t in byPeriod)
            {
                if (!seenSources.Add(t.EmissionSourceId)) continue;
                if (existingSourceIds.Contains(t.EmissionSourceId)) continue;
                if (!byKey.TryGetValue((t.EmissionSourceId, t.PollutantId), out var m)) continue;
                if (m.ExpectedPointsCount == 0) continue; // can't compute availability

                var availability = (decimal)m.ValidPointsCount / m.ExpectedPointsCount;
                if (availability >= _settings.DataAvailabilityThreshold) continue;

                newEvents.Add(ComplianceEvent.ForDataAvailabilityLoss(
                    Guid.NewGuid(), t.EmissionSourceId,
                    measurementId: m.Id, m.WindowStart, m.WindowEnd,
                    notes: $"{m.ValidPointsCount}/{m.ExpectedPointsCount} valid ({availability:P0})"));
            }
        }
        return newEvents;
    }

    // ─── MissingMeasurement ──────────────────────────────────────────────────────

    private async Task<List<ComplianceEvent>> DetectMissingMeasurementAsync(CancellationToken ct)
    {
        var window = TimeSpan.FromMinutes(_settings.MissingMeasurementWindowMinutes);
        var to = DateTime.UtcNow;
        var from = to - window;

        var targets = await GetActiveLimitTargetsAsync(RateBasedLimits, ct);
        var distinctPairs = targets
            .Select(t => (t.EmissionSourceId, t.PollutantId))
            .Distinct()
            .ToList();

        if (distinctPairs.Count == 0) return [];

        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.MissingMeasurement, ct);
        var existingSourceIds = existing.Select(e => e.EmissionSourceId).ToHashSet();

        var sourceIds = distinctPairs.Select(p => p.EmissionSourceId).Distinct().ToArray();
        var pollutantIds = distinctPairs.Select(p => p.PollutantId).Distinct().ToArray();
        var counts = await GetMeasurementCountsAsync(sourceIds, pollutantIds, from, to, ct);

        var newEvents = new List<ComplianceEvent>();
        var reportedSources = new HashSet<Guid>();
        foreach (var pair in distinctPairs)
        {
            if (existingSourceIds.Contains(pair.EmissionSourceId)) continue;
            if (!reportedSources.Add(pair.EmissionSourceId)) continue;

            var count = counts.GetValueOrDefault(pair, 0);
            if (count > 0) continue;

            newEvents.Add(ComplianceEvent.ForMissingMeasurement(
                Guid.NewGuid(), pair.EmissionSourceId, from, to,
                notes: $"No measurements in last {window.TotalMinutes:0} minutes"));
        }
        return newEvents;
    }

    // ─── Shared helpers ──────────────────────────────────────────────────────────

    private record LimitTarget(
        Guid LimitId, Guid EmissionSourceId, Guid PollutantId,
        AveragingWindow Period, decimal Value, Guid UnitId);

    private async Task<List<LimitTarget>> GetActiveLimitTargetsAsync(
        IReadOnlyCollection<LimitType> limitTypes, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var limits = await context.Set<EmissionLimit>()
            .Where(l => limitTypes.Contains(l.LimitType)
                        && l.ValidFrom <= now
                        && (l.ValidTo == null || l.ValidTo >= now)
                        && l.Permit!.PermitStatus == PermitStatus.Active
                        && l.Permit!.ValidUntil >= now)
            .Select(l => new
            {
                l.Id, l.EmissionSourceId, l.InstallationId, l.PollutantId,
                l.Period, l.Value, l.UnitId
            })
            .ToListAsync(ct);

        if (limits.Count == 0) return [];

        var installationIds = limits
            .Where(l => l.EmissionSourceId == null && l.InstallationId != null)
            .Select(l => l.InstallationId!.Value)
            .Distinct()
            .ToArray();

        var sourcesByInstallation = new Dictionary<Guid, List<Guid>>();

        if (installationIds.Length > 0)
        {
            var sources = await context.Set<EmissionSource>()
                .Where(s => installationIds.Contains(s.InstallationId))
                .Select(s => new { s.Id, s.InstallationId })
                .ToListAsync(ct);

            sourcesByInstallation = sources
                .GroupBy(s => s.InstallationId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
        }

        var result = new List<LimitTarget>();
        foreach (var l in limits)
        {
            if (l.EmissionSourceId.HasValue)
            {
                result.Add(new LimitTarget(l.Id, l.EmissionSourceId.Value, l.PollutantId,
                    l.Period, l.Value, l.UnitId));
            }
            else if (l.InstallationId.HasValue
                     && sourcesByInstallation.TryGetValue(l.InstallationId.Value, out var sids))
            {
                foreach (var sid in sids)
                    result.Add(new LimitTarget(l.Id, sid, l.PollutantId, l.Period, l.Value, l.UnitId));
            }
        }
        return result;
    }

    private async Task<Dictionary<Guid, DateTime?>> GetDeviceLastSeenAsync(
        Guid[] deviceIds, CancellationToken ct)
    {
        var sql = @"
            SELECT device_id, MAX(time) AS last_seen
            FROM raw_measurement
            WHERE device_id = ANY(@device_ids)
            GROUP BY device_id";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "device_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, deviceIds);

        var dict = new Dictionary<Guid, DateTime?>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var seen = reader.IsDBNull(1)
                ? (DateTime?)null
                : DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc);
            dict[id] = seen;
        }
        return dict;
    }

    private async Task<Dictionary<(Guid, Guid), long>> GetMeasurementCountsAsync(
        Guid[] sourceIds, Guid[] pollutantIds, DateTime from, DateTime to, CancellationToken ct)
    {
        var sql = @"
            SELECT emission_source_id, pollutant_id, COUNT(*)::bigint
            FROM raw_measurement
            WHERE emission_source_id = ANY(@source_ids)
              AND pollutant_id = ANY(@pollutant_ids)
              AND time >= @from AND time < @to
            GROUP BY emission_source_id, pollutant_id";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, sourceIds);
        AddParam(command, "pollutant_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, pollutantIds);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));

        var dict = new Dictionary<(Guid, Guid), long>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            dict[(reader.GetGuid(0), reader.GetGuid(1))] = reader.GetInt64(2);
        }
        return dict;
    }

    private async Task<NpgsqlCommand> CreateCommandAsync(string sql, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }
        return new NpgsqlCommand(sql, connection);
    }

    private static void AddParam(NpgsqlCommand command, string name, NpgsqlDbType type, object value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.NpgsqlDbType = type;
        p.Value = value;
        command.Parameters.Add(p);
    }

    private static DateTime EnsureUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc);

    private static (DateTime Start, DateTime End) ComputeLastCompletedWindow(AveragingWindow period)
    {
        var ts = PeriodToTimeSpan(period);
        if (ts == TimeSpan.Zero) return (default, default);

        var now = DateTime.UtcNow;
        var floored = new DateTime(now.Ticks - (now.Ticks % ts.Ticks), DateTimeKind.Utc);
        return (floored - ts, floored);
    }

    private static TimeSpan PeriodToTimeSpan(AveragingWindow period) => period switch
    {
        AveragingWindow.Minute1 => TimeSpan.FromMinutes(1),
        AveragingWindow.Minute10 => TimeSpan.FromMinutes(10),
        AveragingWindow.HalfHour => TimeSpan.FromMinutes(30),
        AveragingWindow.Hour1 => TimeSpan.FromHours(1),
        AveragingWindow.Hour24 => TimeSpan.FromHours(24),
        _ => TimeSpan.Zero // Month1, Year1: not handled in real-time detection
    };
}
