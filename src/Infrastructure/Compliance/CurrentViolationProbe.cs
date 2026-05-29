using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Settings;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Domain.Services;
using Microsoft.Extensions.Options;

namespace Infrastructure.Compliance;

/// <summary>
/// Probes whether a previously raised ComplianceEvent still describes a live violation. Bulk
/// implementation: groups events by type, runs one targeted query per type, returns a dict.
/// Reuses the same data primitives as ComplianceDetectionService so probe answers stay
/// consistent with what the detector itself would emit on the next tick.
/// </summary>
public class CurrentViolationProbe(
    IComplianceDetectionQueries queries,
    IOptions<ComplianceDetectionSettings> options) : ICurrentViolationProbe
{
    private readonly ComplianceDetectionSettings _settings = options.Value;

    public async Task<IReadOnlyDictionary<Guid, bool?>> ProbeAsync(
        IReadOnlyCollection<ComplianceEvent> events, CancellationToken ct)
    {
        var result = new Dictionary<Guid, bool?>();
        if (events.Count == 0) return result;

        foreach (var byType in events.GroupBy(e => e.EventType))
        {
            switch (byType.Key)
            {
                case ComplianceEventType.LimitExceedance:
                    await ProbeLimitExceedance(byType, result, ct);
                    break;
                case ComplianceEventType.DeviceOffline:
                    await ProbeDeviceOffline(byType, result, ct);
                    break;
                case ComplianceEventType.CalibrationFailure:
                    await ProbeCalibrationFailure(byType, result, ct);
                    break;
                case ComplianceEventType.DataAvailabilityLoss:
                    await ProbeDataAvailabilityLoss(byType, result, ct);
                    break;
                case ComplianceEventType.MissingMeasurement:
                    await ProbeMissingMeasurement(byType, result, ct);
                    break;
                case ComplianceEventType.OutOfRangeReading:
                    await ProbeOutOfRangeReading(byType, result, ct);
                    break;
            }
        }

        return result;
    }

    // ─── LimitExceedance ────────────────────────────────────────────────────────

    private async Task ProbeLimitExceedance(
        IEnumerable<ComplianceEvent> events,
        Dictionary<Guid, bool?> result,
        CancellationToken ct)
    {
        var eventsArr = events.ToArray();
        var limitIds = eventsArr
            .Where(e => e.LimitId.HasValue)
            .Select(e => e.LimitId!.Value)
            .Distinct()
            .ToArray();

        var limits = await queries.GetActiveLimitsByIdsAsync(limitIds, ct);
        if (limits.Count == 0)
        {
            foreach (var e in eventsArr) result[e.Id] = null;
            return;
        }

        var pairs = eventsArr
            .Where(e => e.LimitId.HasValue && limits.ContainsKey(e.LimitId!.Value))
            .Select(e =>
            {
                var l = limits[e.LimitId!.Value];
                return (SourceId: e.EmissionSourceId, l.PollutantId, l.Period);
            })
            .Distinct()
            .ToList();

        // Single 'now' for the whole probe call so the annual range used for rolling-rate is
        // identical to the annual range used for the flow fetch a few lines later.
        var now = DateTime.UtcNow;

        var byPeriodLatest = new Dictionary<(Guid, Guid, AveragingWindow), MeasurementSnapshot>();
        var byPeriodRolling = new Dictionary<(Guid, Guid, AveragingWindow), RollingAverage>();
        var annualRanges = new Dictionary<AveragingWindow, (DateTime From, DateTime To)>();
        foreach (var byPeriod in pairs.GroupBy(p => p.Period))
        {
            var pairsInPeriod = byPeriod
                .Select(p => (p.SourceId, p.PollutantId))
                .Distinct()
                .ToList();

            if (IsAnnualLoadPeriod(byPeriod.Key))
            {
                var window = AnnualLoadPeriodToTimeSpan(byPeriod.Key);
                if (window == TimeSpan.Zero) continue;
                var sources = pairsInPeriod.Select(p => p.SourceId).Distinct().ToArray();
                var pollutants = pairsInPeriod.Select(p => p.PollutantId).Distinct().ToArray();
                var rolling = await queries.GetRollingAverageRateAsync(
                    sources, pollutants, now - window, now, ct);
                foreach (var (key, r) in rolling)
                    byPeriodRolling[(key.SourceId, key.PollutantId, byPeriod.Key)] = r;
                annualRanges[byPeriod.Key] = (now - window, now);
            }
            else
            {
                var snapshots = await queries.GetLatestMeasurementsAsync(pairsInPeriod, byPeriod.Key, ct);
                foreach (var s in snapshots)
                    byPeriodLatest[(s.EmissionSourceId, s.PollutantId, byPeriod.Key)] = s;
            }
        }

        // Flow fetches mirror the detector exactly. Annual: one call per period over (now - window, now).
        // Non-annual: one call per distinct (WindowStart, WindowEnd) of the latest snapshots so each
        // event compares against the flow of its own window, not a smeared average.
        var annualFlowByPeriod = new Dictionary<AveragingWindow, IReadOnlyDictionary<Guid, FlowReading>>();
        foreach (var (period, range) in annualRanges)
        {
            var sources = byPeriodRolling
                .Where(kvp => kvp.Key.Item3 == period)
                .Select(kvp => kvp.Key.Item1)
                .Distinct()
                .ToArray();
            if (sources.Length == 0) continue;
            annualFlowByPeriod[period] = await queries.GetVolumetricFlowForRangeAsync(
                sources, range.From, range.To, ct);
        }

        var nonAnnualFlowByWindow = new Dictionary<(DateTime Start, DateTime End), IReadOnlyDictionary<Guid, FlowReading>>();
        foreach (var byWindow in byPeriodLatest.Values.GroupBy(s => (s.WindowStart, s.WindowEnd)))
        {
            var sources = byWindow.Select(s => s.EmissionSourceId).Distinct().ToArray();
            nonAnnualFlowByWindow[byWindow.Key] = await queries.GetVolumetricFlowForRangeAsync(
                sources, byWindow.Key.WindowStart, byWindow.Key.WindowEnd, ct);
        }

        // Canonical lookups enable Path 2 (UnitConverter ppm ↔ mg/m³).
        var pollutantIds = limits.Values.Select(l => l.PollutantId).Distinct().ToArray();
        var canonicals = await queries.GetPollutantCanonicalsAsync(pollutantIds, ct);

        var allUnitIds = limits.Values.Select(l => l.UnitId)
            .Concat(byPeriodLatest.Values.Select(s => s.UnitId))
            .Concat(byPeriodRolling.Values.Select(r => r.UnitId))
            .Concat(canonicals.Values.Select(c => c.CanonicalUnitId))
            .Concat(annualFlowByPeriod.Values.SelectMany(d => d.Values.Select(v => v.UnitId)))
            .Concat(nonAnnualFlowByWindow.Values.SelectMany(d => d.Values.Select(v => v.UnitId)))
            .Distinct()
            .ToArray();
        var units = await queries.GetUnitsAsync(allUnitIds, ct);
        var unitEntities = LimitComparisonHelpers.BuildUnitEntities(units);

        foreach (var e in eventsArr)
        {
            if (e.LimitId is null || !limits.TryGetValue(e.LimitId.Value, out var limit))
            {
                result[e.Id] = null;
                continue;
            }
            if (!units.TryGetValue(limit.UnitId, out var limitUnit)
                || !unitEntities.TryGetValue(limit.UnitId, out var limitUnitEntity))
            {
                result[e.Id] = null;
                continue;
            }

            PollutantCanonical? canonical = null;
            MeasureUnit? canonicalUnitEntity = null;
            if (canonicals.TryGetValue(limit.PollutantId, out var c))
            {
                canonical = c;
                unitEntities.TryGetValue(c.CanonicalUnitId, out canonicalUnitEntity);
            }

            var key = (e.EmissionSourceId, limit.PollutantId, limit.Period);
            if (IsAnnualLoadPeriod(limit.Period))
            {
                if (!byPeriodRolling.TryGetValue(key, out var rolling)
                    || !units.TryGetValue(rolling.UnitId, out var measurementUnit)
                    || !unitEntities.TryGetValue(rolling.UnitId, out var measurementUnitEntity))
                {
                    result[e.Id] = null;
                    continue;
                }

                // Path 1 — same dimension on both sides.
                if (measurementUnit.Dimension == limitUnit.Dimension)
                {
                    var measured = rolling.AvgRate * measurementUnit.ToBaseFactor;
                    var limitBase = limit.Value * limitUnit.ToBaseFactor;
                    result[e.Id] = measured > limitBase;
                    continue;
                }

                // Path 2 — UnitConverter cross-dim via pollutant molar mass.
                if (canonical is not null && canonicalUnitEntity is not null
                    && measurementUnit.Dimension == canonicalUnitEntity.Dimension)
                {
                    var effective = rolling.AvgRate;
                    if (rolling.UnitId != canonical.CanonicalUnitId
                        && UnitConverter.TryToCanonical(effective, measurementUnitEntity,
                            canonicalUnitEntity, canonical.MolarMass, out var convertedMeas, out _))
                    {
                        effective = convertedMeas;
                    }
                    if (UnitConverter.TryToCanonical(limit.Value, limitUnitEntity, canonicalUnitEntity,
                            canonical.MolarMass, out var limitCanonical, out _))
                    {
                        result[e.Id] = effective > limitCanonical;
                        continue;
                    }
                }

                // Path 3 — concentration × volumetric flow → mass flow. Uses raw AvgRate
                // because flow and concentration are both at actual stack conditions.
                if (annualFlowByPeriod.TryGetValue(limit.Period, out var annualFlow))
                {
                    var derivedKgPerH = LimitComparisonHelpers.TryDeriveMassFlowKgPerH(
                        rolling.AvgRate, measurementUnit, limitUnit.Dimension,
                        e.EmissionSourceId, annualFlow, units);
                    if (derivedKgPerH is not null)
                    {
                        var limitKgPerH = limit.Value * limitUnit.ToBaseFactor;
                        result[e.Id] = derivedKgPerH.Value > limitKgPerH;
                        continue;
                    }
                }

                result[e.Id] = null;
            }
            else
            {
                if (!byPeriodLatest.TryGetValue(key, out var snapshot)
                    || !units.TryGetValue(snapshot.UnitId, out var measurementUnit)
                    || !unitEntities.TryGetValue(snapshot.UnitId, out var measurementUnitEntity))
                {
                    result[e.Id] = null;
                    continue;
                }
                if (snapshot.Quality != Quality.Valid && snapshot.Quality != Quality.Substituted)
                {
                    result[e.Id] = null;
                    continue;
                }

                var effective = snapshot.NormalizedValue ?? snapshot.Value;

                // Path 1 — same dimension on both sides.
                if (measurementUnit.Dimension == limitUnit.Dimension)
                {
                    var measured = effective * measurementUnit.ToBaseFactor;
                    var limitBase = limit.Value * limitUnit.ToBaseFactor;
                    result[e.Id] = measured > limitBase;
                    continue;
                }

                // Path 2 — UnitConverter cross-dim via pollutant molar mass.
                if (canonical is not null && canonicalUnitEntity is not null
                    && measurementUnit.Dimension == canonicalUnitEntity.Dimension)
                {
                    var effectiveCanonical = effective;
                    if (snapshot.UnitId != canonical.CanonicalUnitId
                        && UnitConverter.TryToCanonical(effective, measurementUnitEntity,
                            canonicalUnitEntity, canonical.MolarMass, out var convertedMeas, out _))
                    {
                        effectiveCanonical = convertedMeas;
                    }
                    if (UnitConverter.TryToCanonical(limit.Value, limitUnitEntity, canonicalUnitEntity,
                            canonical.MolarMass, out var limitCanonical, out _))
                    {
                        result[e.Id] = effectiveCanonical > limitCanonical;
                        continue;
                    }
                }

                // Path 3 — concentration × volumetric flow → mass flow. Uses raw Value (not
                // NormalizedValue) to match the detector — the flow reading is at actual stack
                // conditions, so multiplying it by an O₂-referenced concentration would be wrong.
                if (nonAnnualFlowByWindow.TryGetValue((snapshot.WindowStart, snapshot.WindowEnd), out var flowByKey))
                {
                    var derivedKgPerH = LimitComparisonHelpers.TryDeriveMassFlowKgPerH(
                        snapshot.Value, measurementUnit, limitUnit.Dimension,
                        e.EmissionSourceId, flowByKey, units);
                    if (derivedKgPerH is not null)
                    {
                        var limitKgPerH = limit.Value * limitUnit.ToBaseFactor;
                        result[e.Id] = derivedKgPerH.Value > limitKgPerH;
                        continue;
                    }
                }

                result[e.Id] = null;
            }
        }
    }

    // ─── DeviceOffline ──────────────────────────────────────────────────────────

    private async Task ProbeDeviceOffline(
        IEnumerable<ComplianceEvent> events,
        Dictionary<Guid, bool?> result,
        CancellationToken ct)
    {
        var eventsArr = events.ToArray();
        var deviceIds = eventsArr
            .Where(e => e.DeviceId.HasValue)
            .Select(e => e.DeviceId!.Value)
            .Distinct()
            .ToArray();
        if (deviceIds.Length == 0)
        {
            foreach (var e in eventsArr) result[e.Id] = null;
            return;
        }

        var now = DateTime.UtcNow;
        var cutoff = now - TimeSpan.FromMinutes(_settings.DeviceOfflineThresholdMinutes);
        var lastSeen = await queries.GetDeviceLastSeenAsync(deviceIds, cutoff, ct);

        foreach (var e in eventsArr)
        {
            if (e.DeviceId is null) { result[e.Id] = null; continue; }
            var seen = lastSeen.GetValueOrDefault(e.DeviceId.Value);
            result[e.Id] = !(seen.HasValue && seen.Value >= cutoff);
        }
    }

    // ─── CalibrationFailure ─────────────────────────────────────────────────────

    private async Task ProbeCalibrationFailure(
        IEnumerable<ComplianceEvent> events,
        Dictionary<Guid, bool?> result,
        CancellationToken ct)
    {
        var eventsArr = events.ToArray();
        var snapshots = await queries.GetDevicesWithLatestCalibrationAsync(ct);
        var byDevice = snapshots.ToDictionary(s => s.DeviceId);
        var now = DateTime.UtcNow;

        foreach (var e in eventsArr)
        {
            if (e.DeviceId is null || !byDevice.TryGetValue(e.DeviceId.Value, out var s))
            {
                result[e.Id] = null;
                continue;
            }
            if (s.LastResult is null)
            {
                result[e.Id] = true; // still no calibration at all
                continue;
            }
            var failed = s.LastResult == CalibrationResult.Fail;
            var overdue = s.LastNextDueAt.HasValue && s.LastNextDueAt.Value < now;
            result[e.Id] = failed || overdue;
        }
    }

    // ─── DataAvailabilityLoss ───────────────────────────────────────────────────

    private async Task ProbeDataAvailabilityLoss(
        IEnumerable<ComplianceEvent> events,
        Dictionary<Guid, bool?> result,
        CancellationToken ct)
    {
        // Window-local probe: re-read the event's own Measurement. The materializer updates
        // the measurement row in place, so this gives the current availability for that
        // exact window. Previously the probe used the latest measurement for the
        // (source, pollutant, period) tuple, which lit up "Currently violating" on an
        // event whose own window had self-healed but a later window had gone bad. The
        // detector now creates a separate event for that later window (per-measurement
        // dedup), so probing the original window is both accurate and consistent.
        var eventsArr = events.ToArray();
        var measurementIds = eventsArr
            .Where(e => e.MeasurementId.HasValue)
            .Select(e => e.MeasurementId!.Value)
            .Distinct()
            .ToArray();

        if (measurementIds.Length == 0)
        {
            foreach (var e in eventsArr) result[e.Id] = null;
            return;
        }

        var measurements = await queries.GetMeasurementsByIdsAsync(measurementIds, ct);
        var byId = measurements.ToDictionary(m => m.Id);

        foreach (var e in eventsArr)
        {
            if (e.MeasurementId is null
                || !byId.TryGetValue(e.MeasurementId.Value, out var m)
                || m.ExpectedPointsCount == 0)
            {
                result[e.Id] = null;
                continue;
            }
            var availability = (decimal)m.ValidPointsCount / m.ExpectedPointsCount;
            result[e.Id] = availability < _settings.DataAvailabilityThreshold;
        }
    }

    // ─── MissingMeasurement ─────────────────────────────────────────────────────

    private async Task ProbeMissingMeasurement(
        IEnumerable<ComplianceEvent> events,
        Dictionary<Guid, bool?> result,
        CancellationToken ct)
    {
        var eventsArr = events.ToArray();
        var sourceIds = eventsArr.Select(e => e.EmissionSourceId).Distinct().ToArray();
        var now = DateTime.UtcNow;
        var from = now - TimeSpan.FromMinutes(_settings.MissingMeasurementWindowMinutes);

        var counts = await queries.GetRawMeasurementCountsBySourceAsync(sourceIds, from, now, ct);

        foreach (var e in eventsArr)
        {
            // Still violating if no raw_measurement of any pollutant has been ingested for this
            // source in the latest window.
            result[e.Id] = counts.GetValueOrDefault(e.EmissionSourceId, 0) == 0;
        }
    }

    // ─── OutOfRangeReading ──────────────────────────────────────────────────────

    private async Task ProbeOutOfRangeReading(
        IEnumerable<ComplianceEvent> events,
        Dictionary<Guid, bool?> result,
        CancellationToken ct)
    {
        var eventsArr = events.ToArray();
        var now = DateTime.UtcNow;
        var from = now - TimeSpan.FromMinutes(Math.Max(1, _settings.OutOfRangeWindowMinutes));

        // Re-run the same query the detector uses. Any (source, device) still over threshold
        // is still violating; the rest are not.
        var windows = await queries.GetOutOfRangeWindowsAsync(
            from, now, _settings.OutOfRangeThreshold, _settings.OutOfRangeMinSampleCount, ct);
        var stillViolating = windows
            .Select(w => (w.SourceId, w.DeviceId))
            .ToHashSet();

        foreach (var e in eventsArr)
        {
            if (e.DeviceId is null) { result[e.Id] = null; continue; }
            result[e.Id] = stillViolating.Contains((e.EmissionSourceId, e.DeviceId.Value));
        }
    }

    private static bool IsAnnualLoadPeriod(AveragingWindow p) =>
        p is AveragingWindow.Month1 or AveragingWindow.Year1;

    private static TimeSpan AnnualLoadPeriodToTimeSpan(AveragingWindow period) => period switch
    {
        AveragingWindow.Month1 => TimeSpan.FromDays(30),
        AveragingWindow.Year1 => TimeSpan.FromDays(365),
        _ => TimeSpan.Zero
    };
}
