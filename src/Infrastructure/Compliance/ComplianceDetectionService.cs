using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Settings;
using Application.Features.ComplianceEvents.Notifications;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Compliance;

/// <summary>
/// Orchestrator that runs the 5 fast detectors + AnnualLoad on its own cadence.
/// All DB reads go through IComplianceDetectionQueries; writes through
/// IComplianceEventRepository + IUnitOfWork. No DbContext here.
/// </summary>
public class ComplianceDetectionService(
    IComplianceDetectionQueries queries,
    IComplianceEventRepository complianceEventRepository,
    IComplianceEventQueries complianceEventQueries,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    IOptions<ComplianceDetectionSettings> options,
    ILogger<ComplianceDetectionService> logger)
{
    private readonly ComplianceDetectionSettings _settings = options.Value;
    private static readonly LimitType[] RateBasedLimits = [LimitType.Concentration, LimitType.MassFlow];

    /// <summary>Fast-cadence detectors. Run every tick (5 min by default).</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var newEvents = new List<ComplianceEvent>();

        newEvents.AddRange(await DetectLimitExceedancesAsync(cancellationToken));
        newEvents.AddRange(await DetectDeviceOfflineAsync(cancellationToken));
        newEvents.AddRange(await DetectDataAvailabilityLossAsync(cancellationToken));
        newEvents.AddRange(await DetectMissingMeasurementAsync(cancellationToken));
        newEvents.AddRange(await DetectOutOfRangeReadingsAsync(cancellationToken));

        await PersistAsync(newEvents, cancellationToken);

        logger.LogInformation(
            "Compliance detection: {New} new events in {Ms}ms",
            newEvents.Count, (DateTime.UtcNow - start).TotalMilliseconds);
    }

    /// <summary>Slow-cadence AnnualLoad detector. Annual rolling averages move slowly.</summary>
    public async Task RunAnnualLoadAsync(CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var newEvents = await DetectAnnualLoadExceedancesAsync(cancellationToken);
        await PersistAsync(newEvents, cancellationToken);
        logger.LogInformation(
            "AnnualLoad detection: {New} new events in {Ms}ms",
            newEvents.Count, (DateTime.UtcNow - start).TotalMilliseconds);
    }

    /// <summary>
    /// Slow-cadence calibration check. Calibration records change weekly/monthly and overdue
    /// crossings occur once per day, so checking every fast tick is wasteful.
    /// </summary>
    public async Task RunCalibrationChecksAsync(CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var newEvents = await DetectCalibrationFailuresAsync(cancellationToken);
        await PersistAsync(newEvents, cancellationToken);
        logger.LogInformation(
            "Calibration check: {New} new events in {Ms}ms",
            newEvents.Count, (DateTime.UtcNow - start).TotalMilliseconds);
    }

    private async Task PersistAsync(List<ComplianceEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;
        await complianceEventRepository.AddRangeAsync(events, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Publish AFTER successful save so handlers operate on persisted events. The handlers
        // enqueue Hangfire jobs which are durable on their own — if Hangfire enqueue itself
        // fails, we lose the notification but the ComplianceEvent stays in DB; later phases
        // can replace this with a transactional outbox if loss becomes unacceptable.
        foreach (var ev in events)
        {
            await publisher.Publish(new ComplianceEventOpenedNotification(ev.Id), ct);
        }
    }

    // ─── LimitExceedance ─────────────────────────────────────────────────────────

    private async Task<List<ComplianceEvent>> DetectLimitExceedancesAsync(CancellationToken ct)
    {
        var targets = await queries.GetActiveLimitTargetsAsync(RateBasedLimits, ct);
        if (targets.Count == 0) return [];

        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.LimitExceedance, ct);
        var existingKeys = existing
            .Where(e => e.LimitId.HasValue)
            .Select(e => (e.LimitId!.Value, e.EmissionSourceId))
            .ToHashSet();

        var newEvents = new List<ComplianceEvent>();
        var rescanWindows = Math.Max(0, _settings.LateArrivingRescanWindows);

        foreach (var byPeriod in targets.GroupBy(t => t.Period))
        {
            var (_, lastWindowEnd) = ComputeLastCompletedWindow(byPeriod.Key);
            if (lastWindowEnd == default) continue;

            var periodSpan = PeriodToTimeSpan(byPeriod.Key);
            if (periodSpan == TimeSpan.Zero) continue;

            var sourceIds = byPeriod.Select(t => t.EmissionSourceId).Distinct().ToArray();
            var pollutantIds = byPeriod.Select(t => t.PollutantId).Distinct().ToArray();

            // Scan the last completed window plus the configured rescan tail so windows whose
            // Measurement was rewritten by late-arriving raw data (materialization rescan) still
            // get evaluated against the limit. Iterate newest-first and mark (limit, source)
            // pairs as taken after firing so an older rescan window doesn't double-open.
            var earliestWindowEnd = lastWindowEnd - TimeSpan.FromTicks(periodSpan.Ticks * rescanWindows);
            var allMeasurements = await queries.GetMeasurementsForWindowRangeAsync(
                sourceIds, pollutantIds, byPeriod.Key, earliestWindowEnd, lastWindowEnd, ct);
            if (allMeasurements.Count == 0) continue;

            var unitIds = byPeriod.Select(t => t.UnitId)
                .Concat(allMeasurements.Select(m => m.UnitId))
                .Distinct()
                .ToArray();
            var units = await queries.GetUnitsAsync(unitIds, ct);

            var needsFlow = byPeriod.Any(t =>
                units.TryGetValue(t.UnitId, out var u)
                && u.Dimension == MeasureUnitDimension.MassFlow);

            var orderedWindows = allMeasurements
                .GroupBy(m => m.WindowEnd)
                .OrderByDescending(g => g.Key)
                .ToList();

            foreach (var byWindow in orderedWindows)
            {
                var windowEnd = byWindow.Key;
                var windowStart = windowEnd - periodSpan;
                var byKey = byWindow.ToDictionary(m => (m.EmissionSourceId, m.PollutantId));

                // Volumetric flow is averaged over the matching window only — using a wider range
                // for the whole rescan span would smear flow data across non-comparable windows.
                var flowByKey = needsFlow
                    ? await queries.GetVolumetricFlowForRangeAsync(
                        sourceIds, windowStart, windowEnd, ct)
                    : new Dictionary<Guid, FlowReading>();
                if (flowByKey.Count > 0)
                {
                    var extra = await queries.GetUnitsAsync(
                        flowByKey.Values.Select(v => v.UnitId).Distinct().ToArray(), ct);
                    foreach (var (uid, info) in extra) units.TryAdd(uid, info);
                }

                var aggregateLimitIds = ProcessInstallationAggregates(
                    byPeriod.ToList(), byKey, units, flowByKey, existingKeys, windowEnd, newEvents);

                foreach (var t in byPeriod)
                {
                    if (aggregateLimitIds.Contains(t.LimitId)) continue; // handled above
                    if (existingKeys.Contains((t.LimitId, t.EmissionSourceId))) continue;
                    if (!byKey.TryGetValue((t.EmissionSourceId, t.PollutantId), out var m)) continue;
                    // Allow Valid and Substituted — both are IED-acceptable regulatory values.
                    // Invalid/Missing/Calibration/Maintenance are skipped.
                    if (m.Quality != Quality.Valid && m.Quality != Quality.Substituted) continue;
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
                        existingKeys.Add((t.LimitId, t.EmissionSourceId));
                        continue;
                    }

                    // For Concentration limits, regulator expresses limits at reference conditions
                    // (e.g. "200 mg/m³ NOx @ 6% O₂"). Prefer NormalizedValue when available.
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
                    existingKeys.Add((t.LimitId, t.EmissionSourceId));
                }
            }
        }
        return newEvents;
    }

    /// <summary>
    /// Handles installation-level MassFlow limits by summing values across all sources of the
    /// installation and comparing the total with the limit. Sources reporting MassConcentration
    /// instead of MassFlow are derived via TryDeriveMassFlow (concentration × volumetric flow).
    /// Sources that can be neither matched nor derived are excluded from the sum but the rest
    /// of the aggregate still proceeds, matching the per-source detector's behaviour.
    /// Returns the set of LimitIds it processed so the caller can skip them in the per-source
    /// loop. Concentration limits are skipped (intensive — handled per-source instead).
    /// </summary>
    private HashSet<Guid> ProcessInstallationAggregates(
        IReadOnlyList<LimitTarget> targetsInPeriod,
        IReadOnlyDictionary<(Guid, Guid), MeasurementSnapshot> measurementByKey,
        IReadOnlyDictionary<Guid, UnitInfo> units,
        IReadOnlyDictionary<Guid, FlowReading> flowByKey,
        HashSet<(Guid LimitId, Guid EmissionSourceId)> existingKeys,
        DateTime windowEnd,
        List<ComplianceEvent> sink)
    {
        var processed = new HashSet<Guid>();

        var aggregateGroups = targetsInPeriod
            .Where(t => t.InstallationId.HasValue && t.LimitType == LimitType.MassFlow)
            .GroupBy(t => t.LimitId);

        foreach (var byLimit in aggregateGroups)
        {
            processed.Add(byLimit.Key);
            var primary = byLimit.First();
            if (!units.TryGetValue(primary.UnitId, out var limitUnit)) continue;
            if (existingKeys.Any(k => k.LimitId == primary.LimitId)) continue; // dedup

            decimal sumBase = 0m;
            var contributingSources = new List<Guid>();
            var derivedCount = 0;
            var excludedCount = 0;
            DateTime? windowStart = null;

            foreach (var t in byLimit)
            {
                if (!measurementByKey.TryGetValue((t.EmissionSourceId, t.PollutantId), out var m)) continue;
                if (m.Quality != Quality.Valid && m.Quality != Quality.Substituted) continue;
                if (!units.TryGetValue(m.UnitId, out var measurementUnit)) continue;

                if (measurementUnit.Dimension == limitUnit.Dimension)
                {
                    var effectiveValue = m.NormalizedValue ?? m.Value;
                    sumBase += effectiveValue * measurementUnit.ToBaseFactor;
                    contributingSources.Add(t.EmissionSourceId);
                    windowStart = m.WindowStart;
                    continue;
                }

                var derived = TryDeriveMassFlow(t, m.Value, limitUnit, measurementUnit, flowByKey, units);
                if (derived is null)
                {
                    logger.LogWarning(
                        "Installation-aggregate limit {LimitId}: source {SourceId} uses {MeasDim} " +
                        "with no derivation path to {LimitDim}; excluded from sum.",
                        primary.LimitId, t.EmissionSourceId, measurementUnit.Dimension, limitUnit.Dimension);
                    excludedCount++;
                    continue;
                }

                // TryDeriveMassFlow already returns kg/h (the MassFlow base unit), so no further
                // factor conversion is needed.
                sumBase += derived.MassFlowKgPerH;
                contributingSources.Add(t.EmissionSourceId);
                derivedCount++;
                windowStart = m.WindowStart;
            }

            if (contributingSources.Count == 0) continue;

            var limitBase = primary.Value * limitUnit.ToBaseFactor;
            if (sumBase <= limitBase) continue;

            var ratio = Math.Round(sumBase / limitBase, 4);
            var fidelityTail = AggregateFidelityNote(contributingSources.Count, derivedCount, excludedCount);
            sink.Add(ComplianceEvent.ForLimitExceedance(
                Guid.NewGuid(),
                emissionSourceId: contributingSources[0], // representative; events require a single source
                measurementId: null,
                limitId: primary.LimitId,
                ratio: ratio,
                windowStart: windowStart!.Value,
                windowEnd: windowEnd,
                notes: $"Installation aggregate: sum {sumBase:0.###} {limitUnit.Symbol} " +
                       $"across {contributingSources.Count} source(s){fidelityTail} > " +
                       $"{primary.Value:0.###} {limitUnit.Symbol} (ratio {ratio:0.##})"));
            // Mark this aggregate limit as taken so the rescan loop's older windows skip it via
            // the existingKeys.Any(...) check at the top of this helper.
            existingKeys.Add((primary.LimitId, contributingSources[0]));
        }

        return processed;
    }

    private static string AggregateFidelityNote(int contributing, int derived, int excluded)
    {
        if (derived == 0 && excluded == 0) return "";
        var parts = new List<string>();
        if (derived > 0) parts.Add($"{derived} derived from concentration×flow");
        if (excluded > 0) parts.Add($"{excluded} excluded (no derivation data)");
        return $" [{string.Join("; ", parts)}]";
    }

    private record DerivedMassFlow(decimal MassFlowKgPerH, decimal LimitKgPerH, string FlowDescription);

    private static DerivedMassFlow? TryDeriveMassFlow(
        LimitTarget t, decimal measurementValue,
        UnitInfo limitUnit, UnitInfo measurementUnit,
        IReadOnlyDictionary<Guid, FlowReading> flowByKey,
        IReadOnlyDictionary<Guid, UnitInfo> units)
    {
        if (limitUnit.Dimension != MeasureUnitDimension.MassFlow) return null;
        if (measurementUnit.Dimension != MeasureUnitDimension.MassConcentration) return null;
        if (!flowByKey.TryGetValue(t.EmissionSourceId, out var flow)) return null;
        if (!units.TryGetValue(flow.UnitId, out var flowUnit)) return null;
        if (flowUnit.Dimension != MeasureUnitDimension.VolumetricFlow) return null;

        // (mg/m³ base) × (m³/h base) = mg/h → /1e6 = kg/h
        var concBase = measurementValue * measurementUnit.ToBaseFactor;
        var flowBase = flow.Value * flowUnit.ToBaseFactor;
        var massFlowKgPerH = (concBase * flowBase) / 1_000_000m;
        var limitKgPerH = t.Value * limitUnit.ToBaseFactor;
        return new DerivedMassFlow(
            MassFlowKgPerH: Math.Round(massFlowKgPerH, 6),
            LimitKgPerH: limitKgPerH,
            FlowDescription: $"{flow.Value:0.###} {flowUnit.Symbol}");
    }

    // ─── AnnualLoad ──────────────────────────────────────────────────────────────

    private async Task<List<ComplianceEvent>> DetectAnnualLoadExceedancesAsync(CancellationToken ct)
    {
        var targets = await queries.GetActiveLimitTargetsAsync([LimitType.AnnualLoad], ct);
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

            var rolling = await queries.GetRollingAverageRateAsync(sourceIds, pollutantIds, from, now, ct);
            if (rolling.Count == 0) continue;

            var unitIds = byPeriod.Select(t => t.UnitId)
                .Concat(rolling.Values.Select(r => r.UnitId))
                .Distinct()
                .ToArray();
            var units = await queries.GetUnitsAsync(unitIds, ct);

            var needsFlow = byPeriod.Any(t =>
                units.TryGetValue(t.UnitId, out var u)
                && u.Dimension == MeasureUnitDimension.MassFlow);
            var flowByKey = needsFlow
                ? await queries.GetVolumetricFlowForRangeAsync(sourceIds, from, now, ct)
                : new Dictionary<Guid, FlowReading>();
            if (flowByKey.Count > 0)
            {
                var extra = await queries.GetUnitsAsync(
                    flowByKey.Values.Select(v => v.UnitId).Distinct().ToArray(), ct);
                foreach (var (uid, info) in extra) units.TryAdd(uid, info);
            }

            // For Concentration AnnualLoad limits, regulator references "@O₂_ref" basis.
            // Pre-fetch year-averaged O₂ per source + pollutant O₂ refs to apply IED normalization.
            var pollutantO2Refs = await queries.GetPollutantO2ReferencesAsync(pollutantIds, ct);
            var needsNormalization = pollutantO2Refs.Values.Any(v => v.HasValue)
                && byPeriod.Any(t =>
                    units.TryGetValue(t.UnitId, out var u)
                    && u.Dimension == MeasureUnitDimension.MassConcentration);
            var o2Avgs = needsNormalization
                ? await queries.GetO2AverageForRangeAsync(sourceIds, from, now, ct)
                : new Dictionary<Guid, decimal>();

            // Installation-level AnnualLoad: sum rolling-average rates across all sources of
            // the installation and compare once.
            var aggregateLimitIds = ProcessAnnualLoadAggregates(
                byPeriod.ToList(), rolling, units, flowByKey, existingKeys,
                pollutantO2Refs, o2Avgs, from, now, window, newEvents);

            foreach (var t in byPeriod)
            {
                if (aggregateLimitIds.Contains(t.LimitId)) continue;
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

                var normalizationApplied = false;
                var effectiveRate = r.AvgRate;
                if (limitUnit.Dimension == MeasureUnitDimension.MassConcentration)
                {
                    var normalized = TryComputeAnnualO2Normalization(
                        r.AvgRate, pollutantO2Refs.GetValueOrDefault(t.PollutantId),
                        o2Avgs.GetValueOrDefault(t.EmissionSourceId));
                    if (normalized.HasValue)
                    {
                        effectiveRate = normalized.Value;
                        normalizationApplied = true;
                    }
                }

                var measuredBase = effectiveRate * measurementUnit.ToBaseFactor;
                var limitBase = t.Value * limitUnit.ToBaseFactor;
                if (measuredBase <= limitBase) continue;

                var ratio = Math.Round(measuredBase / limitBase, 4);
                var label = normalizationApplied
                    ? $"avg {effectiveRate:0.###} {measurementUnit.Symbol} (normalized)"
                    : $"avg {r.AvgRate:0.###} {measurementUnit.Symbol}";
                newEvents.Add(ComplianceEvent.ForLimitExceedance(
                    Guid.NewGuid(), t.EmissionSourceId,
                    measurementId: null, t.LimitId, ratio, from, now,
                    notes: $"AnnualLoad: {label} over last {window.TotalDays:0}d > " +
                           $"limit {t.Value:0.###} {limitUnit.Symbol} (ratio {ratio:0.##}, {r.Samples} samples)"));
            }
        }
        return newEvents;
    }

    /// <summary>
    /// Year-averaged IED O₂ correction for AnnualLoad concentration limits.
    /// Less precise than per-minute normalisation (since O₂ varies during the year) but
    /// is the standard practice for long-window averages.
    /// </summary>
    private static decimal? TryComputeAnnualO2Normalization(
        decimal rawAvgRate, decimal? o2Reference, decimal o2Actual)
    {
        if (o2Reference is null) return null;
        if (o2Actual >= 21m || o2Actual < 0.5m) return null;
        if (o2Actual == 0m) return null;
        return Math.Round(rawAvgRate * (21m - o2Reference.Value) / (21m - o2Actual), 6);
    }

    /// <summary>
    /// Installation-level AnnualLoad aggregation: sum rolling average rates across all sources of
    /// the installation, compare with limit once. Returns the LimitIds it processed so the caller
    /// can skip them in the per-source loop.
    /// </summary>
    private HashSet<Guid> ProcessAnnualLoadAggregates(
        IReadOnlyList<LimitTarget> targetsInPeriod,
        IReadOnlyDictionary<(Guid, Guid), RollingAverage> rollingByKey,
        IReadOnlyDictionary<Guid, UnitInfo> units,
        IReadOnlyDictionary<Guid, FlowReading> flowByKey,
        HashSet<(Guid LimitId, Guid EmissionSourceId)> existingKeys,
        IReadOnlyDictionary<Guid, decimal?> pollutantO2Refs,
        IReadOnlyDictionary<Guid, decimal> o2Avgs,
        DateTime from, DateTime to, TimeSpan window,
        List<ComplianceEvent> sink)
    {
        var processed = new HashSet<Guid>();

        var aggregateGroups = targetsInPeriod
            .Where(t => t.InstallationId.HasValue && t.LimitType == LimitType.AnnualLoad)
            .GroupBy(t => t.LimitId);

        foreach (var byLimit in aggregateGroups)
        {
            processed.Add(byLimit.Key);
            var primary = byLimit.First();
            if (!units.TryGetValue(primary.UnitId, out var limitUnit)) continue;
            if (existingKeys.Any(k => k.LimitId == primary.LimitId)) continue;

            decimal sumBase = 0m;
            var contributingSources = new List<Guid>();
            long totalSamples = 0;
            var anyNormalized = false;
            var derivedCount = 0;
            var excludedCount = 0;

            foreach (var t in byLimit)
            {
                if (!rollingByKey.TryGetValue((t.EmissionSourceId, t.PollutantId), out var r)) continue;
                if (!units.TryGetValue(r.UnitId, out var measurementUnit)) continue;

                if (measurementUnit.Dimension == limitUnit.Dimension)
                {
                    var rateForSource = r.AvgRate;
                    if (limitUnit.Dimension == MeasureUnitDimension.MassConcentration)
                    {
                        var normalized = TryComputeAnnualO2Normalization(
                            r.AvgRate,
                            pollutantO2Refs.GetValueOrDefault(t.PollutantId),
                            o2Avgs.GetValueOrDefault(t.EmissionSourceId));
                        if (normalized.HasValue)
                        {
                            rateForSource = normalized.Value;
                            anyNormalized = true;
                        }
                    }

                    sumBase += rateForSource * measurementUnit.ToBaseFactor;
                    totalSamples += r.Samples;
                    contributingSources.Add(t.EmissionSourceId);
                    continue;
                }

                // Cross-dimension: try derivation (only MassConcentration → MassFlow path supported).
                var derived = TryDeriveMassFlow(t, r.AvgRate, limitUnit, measurementUnit, flowByKey, units);
                if (derived is null)
                {
                    logger.LogWarning(
                        "AnnualLoad aggregate {LimitId}: source {SourceId} uses {MeasDim} with no " +
                        "derivation path to {LimitDim}; excluded from sum.",
                        primary.LimitId, t.EmissionSourceId, measurementUnit.Dimension, limitUnit.Dimension);
                    excludedCount++;
                    continue;
                }

                sumBase += derived.MassFlowKgPerH;
                totalSamples += r.Samples;
                contributingSources.Add(t.EmissionSourceId);
                derivedCount++;
            }

            if (contributingSources.Count == 0) continue;

            var limitBase = primary.Value * limitUnit.ToBaseFactor;
            if (sumBase <= limitBase) continue;

            var ratio = Math.Round(sumBase / limitBase, 4);
            var normalizedLabel = anyNormalized ? " (normalized)" : "";
            var fidelityTail = AggregateFidelityNote(contributingSources.Count, derivedCount, excludedCount);
            sink.Add(ComplianceEvent.ForLimitExceedance(
                Guid.NewGuid(),
                emissionSourceId: contributingSources[0],
                measurementId: null,
                limitId: primary.LimitId,
                ratio: ratio,
                windowStart: from,
                windowEnd: to,
                notes: $"AnnualLoad installation aggregate: sum {sumBase:0.###} {limitUnit.Symbol}{normalizedLabel} " +
                       $"across {contributingSources.Count} source(s){fidelityTail} over last {window.TotalDays:0}d > " +
                       $"{primary.Value:0.###} {limitUnit.Symbol} (ratio {ratio:0.##}, {totalSamples} samples)"));
        }

        return processed;
    }

    // ─── DeviceOffline ───────────────────────────────────────────────────────────

    private async Task<List<ComplianceEvent>> DetectDeviceOfflineAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var threshold = TimeSpan.FromMinutes(_settings.DeviceOfflineThresholdMinutes);
        var cutoff = now - threshold;
        var graceLine = now - TimeSpan.FromDays(Math.Max(0, _settings.NewDeviceGraceDays));

        var devices = await queries.GetOperationalDevicesAsync(ct);
        if (devices.Count == 0) return [];

        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.DeviceOffline, ct);
        var existingDeviceIds = existing
            .Where(e => e.DeviceId.HasValue)
            .Select(e => e.DeviceId!.Value)
            .ToHashSet();

        var lastSeen = await queries.GetDeviceLastSeenAsync(
            devices.Select(d => d.Id).ToArray(), ct);

        var newEvents = new List<ComplianceEvent>();
        foreach (var d in devices)
        {
            if (existingDeviceIds.Contains(d.Id)) continue;
            if (d.InstalledAt.HasValue && d.InstalledAt.Value > graceLine) continue;

            var seen = lastSeen.GetValueOrDefault(d.Id);
            if (seen.HasValue && seen.Value >= cutoff) continue;

            newEvents.Add(ComplianceEvent.ForDeviceOffline(
                Guid.NewGuid(), d.EmissionSourceId, d.Id,
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

        var snapshots = await queries.GetDevicesWithLatestCalibrationAsync(ct);
        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.CalibrationFailure, ct);
        var existingDeviceIds = existing
            .Where(e => e.DeviceId.HasValue)
            .Select(e => e.DeviceId!.Value)
            .ToHashSet();

        var newEvents = new List<ComplianceEvent>();
        foreach (var s in snapshots)
        {
            if (existingDeviceIds.Contains(s.DeviceId)) continue;

            if (s.LastResult is null)
            {
                // No calibration ever — alert only after grace period.
                if (s.InstalledAt is null || s.InstalledAt.Value > graceLine) continue;

                newEvents.Add(ComplianceEvent.ForCalibrationFailure(
                    Guid.NewGuid(), s.EmissionSourceId, s.DeviceId,
                    s.InstalledAt.Value, now,
                    notes: $"No calibration record found; device installed {s.InstalledAt.Value:O}"));
                continue;
            }

            var failed = s.LastResult == CalibrationResult.Fail;
            var overdue = s.LastNextDueAt < now;
            if (!failed && !overdue) continue;

            newEvents.Add(ComplianceEvent.ForCalibrationFailure(
                Guid.NewGuid(), s.EmissionSourceId, s.DeviceId,
                s.LastNextDueAt!.Value, now,
                notes: failed
                    ? $"Last calibration {s.LastPerformedAt:O} returned Fail"
                    : $"Calibration overdue since {s.LastNextDueAt:O}"));
        }
        return newEvents;
    }

    // ─── DataAvailabilityLoss ────────────────────────────────────────────────────

    private async Task<List<ComplianceEvent>> DetectDataAvailabilityLossAsync(CancellationToken ct)
    {
        var targets = await queries.GetActiveLimitTargetsAsync(RateBasedLimits, ct);
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
            var measurements = await queries.GetMeasurementsForWindowAsync(
                sourceIds, pollutantIds, byPeriod.Key, to, ct);
            var byKey = measurements.ToDictionary(m => (m.EmissionSourceId, m.PollutantId));

            foreach (var t in byPeriod)
            {
                if (!seenSources.Add(t.EmissionSourceId)) continue;
                if (existingSourceIds.Contains(t.EmissionSourceId)) continue;
                if (!byKey.TryGetValue((t.EmissionSourceId, t.PollutantId), out var m)) continue;
                if (m.ExpectedPointsCount == 0) continue;

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

        var targets = await queries.GetActiveLimitTargetsAsync(RateBasedLimits, ct);
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
        var counts = await queries.GetRawMeasurementCountsAsync(sourceIds, pollutantIds, from, to, ct);

        var newEvents = new List<ComplianceEvent>();
        var reported = new HashSet<Guid>();
        foreach (var pair in distinctPairs)
        {
            if (existingSourceIds.Contains(pair.EmissionSourceId)) continue;
            if (!reported.Add(pair.EmissionSourceId)) continue;
            if (counts.GetValueOrDefault(pair, 0) > 0) continue;

            newEvents.Add(ComplianceEvent.ForMissingMeasurement(
                Guid.NewGuid(), pair.EmissionSourceId, from, to,
                notes: $"No measurements in last {window.TotalMinutes:0} minutes"));
        }
        return newEvents;
    }

    // ─── OutOfRangeReading ───────────────────────────────────────────────────────

    private async Task<List<ComplianceEvent>> DetectOutOfRangeReadingsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var windowMinutes = Math.Max(1, _settings.OutOfRangeWindowMinutes);
        var from = now - TimeSpan.FromMinutes(windowMinutes);

        var windows = await queries.GetOutOfRangeWindowsAsync(
            from, now, _settings.OutOfRangeThreshold, _settings.OutOfRangeMinSampleCount, ct);
        if (windows.Count == 0) return [];

        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.OutOfRangeReading, ct);
        // Dedup at (source, device) — ComplianceEvent currently has no PollutantId column so
        // multiple pollutants drifting on the same device collapse into a single event.
        // Pollutant is recorded in Notes for triage.
        var existingKeys = existing
            .Where(e => e.DeviceId.HasValue)
            .Select(e => (e.EmissionSourceId, e.DeviceId!.Value))
            .ToHashSet();

        var newEvents = new List<ComplianceEvent>();
        var added = new HashSet<(Guid SourceId, Guid DeviceId)>();
        foreach (var w in windows)
        {
            var key = (w.SourceId, w.DeviceId);
            if (existingKeys.Contains(key)) continue;
            if (!added.Add(key)) continue;

            newEvents.Add(ComplianceEvent.ForOutOfRangeReading(
                Guid.NewGuid(), w.SourceId, w.DeviceId, w.InvalidRatio, from, now,
                notes: $"Pollutant {w.PollutantId}: {w.InvalidCount}/{w.Total} readings " +
                       $"({w.InvalidRatio:P0}) out of sensor range over last {windowMinutes} min " +
                       $"(threshold {_settings.OutOfRangeThreshold:P0})"));
        }
        return newEvents;
    }

    // ─── Period helpers ──────────────────────────────────────────────────────────

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
        _ => TimeSpan.Zero // Month1/Year1 handled by AnnualLoadPeriodToTimeSpan
    };

    private static TimeSpan AnnualLoadPeriodToTimeSpan(AveragingWindow period) => period switch
    {
        AveragingWindow.Month1 => TimeSpan.FromDays(30),
        AveragingWindow.Year1 => TimeSpan.FromDays(365),
        _ => TimeSpan.Zero
    };
}
