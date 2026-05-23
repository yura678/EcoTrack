using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Settings;
using Application.Features.ComplianceEvents.Notifications;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Domain.Services;
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

    /// <summary>Fast-cadence detectors across all tenants. Legacy IHostedService entry.</summary>
    public Task RunAsync(CancellationToken cancellationToken) =>
        RunFastInternalAsync(enterpriseId: null, cancellationToken);

    /// <summary>Fast-cadence detectors scoped to a single tenant — Hangfire fan-out entry.</summary>
    public Task RunForEnterpriseAsync(Guid enterpriseId, CancellationToken cancellationToken) =>
        RunFastInternalAsync(enterpriseId, cancellationToken);

    /// <summary>Slow-cadence AnnualLoad detector across all tenants. Legacy entry.</summary>
    public Task RunAnnualLoadAsync(CancellationToken cancellationToken) =>
        RunAnnualLoadInternalAsync(enterpriseId: null, cancellationToken);

    /// <summary>AnnualLoad scoped to a single tenant — Hangfire fan-out entry.</summary>
    public Task RunAnnualLoadForEnterpriseAsync(Guid enterpriseId, CancellationToken cancellationToken) =>
        RunAnnualLoadInternalAsync(enterpriseId, cancellationToken);

    /// <summary>Slow-cadence calibration check across all tenants. Legacy entry.</summary>
    public Task RunCalibrationChecksAsync(CancellationToken cancellationToken) =>
        RunCalibrationChecksInternalAsync(enterpriseId: null, cancellationToken);

    /// <summary>Calibration check scoped to a single tenant — Hangfire fan-out entry.</summary>
    public Task RunCalibrationChecksForEnterpriseAsync(Guid enterpriseId, CancellationToken cancellationToken) =>
        RunCalibrationChecksInternalAsync(enterpriseId, cancellationToken);

    private async Task RunFastInternalAsync(Guid? enterpriseId, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var newEvents = new List<ComplianceEvent>();

        var (exceedances, evaluatedLimitIds) = await DetectLimitExceedancesAsync(cancellationToken, enterpriseId);
        newEvents.AddRange(exceedances);
        newEvents.AddRange(await DetectDeviceOfflineAsync(cancellationToken, enterpriseId));
        newEvents.AddRange(await DetectDataAvailabilityLossAsync(cancellationToken, enterpriseId));
        newEvents.AddRange(await DetectMissingMeasurementAsync(cancellationToken, enterpriseId));
        newEvents.AddRange(await DetectOutOfRangeReadingsAsync(cancellationToken, enterpriseId));

        await PersistAsync(newEvents, cancellationToken);
        var closed = await CloseResolvedUnenforceableLimitsAsync(enterpriseId, evaluatedLimitIds, cancellationToken);

        logger.LogInformation(
            "Compliance detection: {New} new, {Closed} auto-closed in {Ms}ms (enterprise: {Enterprise})",
            newEvents.Count, closed, (DateTime.UtcNow - start).TotalMilliseconds,
            enterpriseId?.ToString() ?? "all");
    }

    private async Task RunAnnualLoadInternalAsync(Guid? enterpriseId, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var (newEvents, evaluatedLimitIds) = await DetectAnnualLoadExceedancesAsync(cancellationToken, enterpriseId);
        await PersistAsync(newEvents, cancellationToken);
        var closed = await CloseResolvedUnenforceableLimitsAsync(enterpriseId, evaluatedLimitIds, cancellationToken);
        logger.LogInformation(
            "AnnualLoad detection: {New} new, {Closed} auto-closed in {Ms}ms (enterprise: {Enterprise})",
            newEvents.Count, closed, (DateTime.UtcNow - start).TotalMilliseconds,
            enterpriseId?.ToString() ?? "all");
    }

    private async Task RunCalibrationChecksInternalAsync(Guid? enterpriseId, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var newEvents = await DetectCalibrationFailuresAsync(cancellationToken, enterpriseId);
        await PersistAsync(newEvents, cancellationToken);
        logger.LogInformation(
            "Calibration check: {New} new events in {Ms}ms (enterprise: {Enterprise})",
            newEvents.Count, (DateTime.UtcNow - start).TotalMilliseconds,
            enterpriseId?.ToString() ?? "all");
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

    /// <summary>
    /// Re-checks every open UnenforceableLimit and closes it when the limit became enforceable
    /// again. Three signals trigger close (in order of preference):
    /// <list type="number">
    ///   <item>The detector successfully evaluated the limit this tick — any of the comparison
    ///         paths (linear, ppm-canonical, derivation) or an existing open exceedance proved
    ///         the limit is being enforced again.</item>
    ///   <item>The limit is no longer active (removed or its permit expired) — there's nothing
    ///         left to enforce, so the stale event has no purpose.</item>
    ///   <item>The limit unit reconciles with the pollutant canonical via <see cref="UnitConverter"/>
    ///         even without any data this tick — closes proactively after the operator fixed
    ///         the unit/MolarMass before any reading proves it.</item>
    /// </list>
    /// </summary>
    private async Task<int> CloseResolvedUnenforceableLimitsAsync(
        Guid? enterpriseId, IReadOnlySet<Guid> evaluatedLimitIds, CancellationToken ct)
    {
        var open = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.UnenforceableLimit, ct);
        if (open.Count == 0) return 0;

        // Per-tenant Hangfire jobs each run this method; scope in-memory so two tenants' jobs
        // don't race to close each other's events.
        if (enterpriseId.HasValue)
        {
            open = open.Where(e => e.EnterpriseId == enterpriseId.Value).ToList();
            if (open.Count == 0) return 0;
        }

        var limitIds = open.Where(e => e.LimitId.HasValue)
            .Select(e => e.LimitId!.Value)
            .Distinct()
            .ToArray();
        var currentLimits = await queries.GetActiveLimitsByIdsAsync(limitIds, ct);

        Dictionary<Guid, PollutantCanonical> canonicals = [];
        Dictionary<Guid, MeasureUnit> unitEntities = [];
        if (currentLimits.Count > 0)
        {
            var pollutantIds = currentLimits.Values.Select(l => l.PollutantId).Distinct().ToArray();
            canonicals = await queries.GetPollutantCanonicalsAsync(pollutantIds, ct);
            var unitIds = currentLimits.Values.Select(l => l.UnitId)
                .Concat(canonicals.Values.Select(c => c.CanonicalUnitId))
                .Distinct()
                .ToArray();
            var units = await queries.GetUnitsAsync(unitIds, ct);
            unitEntities = BuildUnitEntities(units);
        }

        var closedIds = new List<Guid>();
        foreach (var ev in open)
        {
            if (!ev.LimitId.HasValue) continue;

            string? closeNote = null;
            if (evaluatedLimitIds.Contains(ev.LimitId.Value))
            {
                // Detector compared this limit successfully this tick (any path), so whatever
                // previously made it unenforceable has been resolved.
                closeNote = "Limit was successfully evaluated this tick; auto-closed by detector.";
            }
            else if (!currentLimits.TryGetValue(ev.LimitId.Value, out var limit))
            {
                closeNote = "Limit is no longer active (removed or expired); auto-closed by detector.";
            }
            else if (canonicals.TryGetValue(limit.PollutantId, out var canonical)
                     && unitEntities.TryGetValue(limit.UnitId, out var limitUnit)
                     && unitEntities.TryGetValue(canonical.CanonicalUnitId, out var canonicalUnit)
                     && UnitConverter.TryToCanonical(limit.Value, limitUnit, canonicalUnit,
                         canonical.MolarMass, out _, out _))
            {
                closeNote = $"Limit unit {limitUnit.Symbol} now reconciles with pollutant canonical " +
                            $"{canonicalUnit.Symbol}; auto-closed by detector.";
            }

            if (closeNote is null) continue;

            ev.Close(ResolutionReason.OperatorAction, closeNote, resolvedByUserId: null);
            complianceEventRepository.Update(ev);
            closedIds.Add(ev.Id);
        }

        if (closedIds.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(ct);
            // Notify subscribers (SignalR pushes the new Closed state into open browser tabs)
            // AFTER save so handlers always see the persisted resolution metadata. Same ordering
            // discipline as PersistAsync for opened events.
            foreach (var id in closedIds)
            {
                await publisher.Publish(new ComplianceEventClosedNotification(id), ct);
            }
        }
        return closedIds.Count;
    }

    // ─── LimitExceedance ─────────────────────────────────────────────────────────

    private async Task<(List<ComplianceEvent> Events, HashSet<Guid> EvaluatedLimitIds)>
        DetectLimitExceedancesAsync(CancellationToken ct, Guid? enterpriseId = null)
    {
        var targets = await queries.GetActiveLimitTargetsAsync(RateBasedLimits, ct, enterpriseId);
        if (targets.Count == 0) return ([], []);

        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.LimitExceedance, ct);
        var existingKeys = existing
            .Where(e => e.LimitId.HasValue)
            .Select(e => (e.LimitId!.Value, e.EmissionSourceId))
            .ToHashSet();
        var existingUnenforceableLimitIds = (await complianceEventQueries.GetOpenByTypeAsync(
                ComplianceEventType.UnenforceableLimit, ct))
            .Where(e => e.LimitId.HasValue)
            .Select(e => e.LimitId!.Value)
            .ToHashSet();

        var newEvents = new List<ComplianceEvent>();
        // Limits the detector compared against measurement data this tick (via any of paths 1–3,
        // exceedance fired or not, or via the existingKeys short-circuit that proves a prior
        // successful evaluation). Feeds the UnenforceableLimit auto-close so the contradictory
        // "UnenforceableLimit open + LimitExceedance fires on the same limit" state resolves.
        var evaluatedLimitIds = new HashSet<Guid>();
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

            var canonicals = await queries.GetPollutantCanonicalsAsync(pollutantIds, ct);
            var unitIds = byPeriod.Select(t => t.UnitId)
                .Concat(allMeasurements.Select(m => m.UnitId))
                .Concat(canonicals.Values.Select(c => c.CanonicalUnitId))
                .Distinct()
                .ToArray();
            var units = await queries.GetUnitsAsync(unitIds, ct);
            var unitEntities = BuildUnitEntities(units);

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
                    foreach (var (uid, info) in extra)
                    {
                        units.TryAdd(uid, info);
                        unitEntities.TryAdd(uid, MeasureUnit.New(uid, info.Symbol, info.Dimension, info.ToBaseFactor));
                    }
                }

                var aggregateLimitIds = ProcessInstallationAggregates(
                    byPeriod.ToList(), byKey, units, unitEntities, canonicals,
                    flowByKey, existingKeys, existingUnenforceableLimitIds, evaluatedLimitIds,
                    windowStart, windowEnd, newEvents);

                foreach (var t in byPeriod)
                {
                    if (aggregateLimitIds.Contains(t.LimitId)) continue; // handled above
                    if (existingKeys.Contains((t.LimitId, t.EmissionSourceId)))
                    {
                        // Existing open LimitExceedance proves the limit was evaluable at some
                        // point — close any stale UnenforceableLimit alongside it.
                        evaluatedLimitIds.Add(t.LimitId);
                        continue;
                    }
                    if (!byKey.TryGetValue((t.EmissionSourceId, t.PollutantId), out var m)) continue;
                    // Allow Valid and Substituted — both are IED-acceptable regulatory values.
                    // Invalid/Missing/Calibration/Maintenance are skipped.
                    if (m.Quality != Quality.Valid && m.Quality != Quality.Substituted) continue;
                    if (!units.TryGetValue(t.UnitId, out var limitUnit)
                        || !units.TryGetValue(m.UnitId, out var measurementUnit)) continue;
                    if (!canonicals.TryGetValue(t.PollutantId, out var canonical)) continue;
                    if (!unitEntities.TryGetValue(t.UnitId, out var limitUnitEntity)
                        || !unitEntities.TryGetValue(canonical.CanonicalUnitId, out var canonicalUnitEntity)) continue;

                    // Prefer NormalizedValue when set — concentration limits are "@ O₂ reference"
                    // and the materializer computes that value at write time.
                    var effectiveValue = m.NormalizedValue ?? m.Value;

                    // Path 1 — same dimension on both sides. Linear ToBaseFactor compare, works
                    // even if measurement isn't in canonical (legacy/test data). This is the
                    // overwhelmingly common case in production.
                    if (measurementUnit.Dimension == limitUnit.Dimension)
                    {
                        evaluatedLimitIds.Add(t.LimitId);
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
                        continue;
                    }

                    // Path 2 — cross-dimension via UnitConverter (ppm → MassConcentration via
                    // molar mass). Requires measurement to share canonical's dimension; defensively
                    // convert into canonical first when the measurement is in a non-canonical unit
                    // of the same dimension (pre-Phase-2 history / test fixtures).
                    if (measurementUnit.Dimension == canonicalUnitEntity.Dimension)
                    {
                        if (m.UnitId != canonical.CanonicalUnitId
                            && UnitConverter.TryToCanonical(effectiveValue, unitEntities[m.UnitId],
                                canonicalUnitEntity, canonical.MolarMass, out var convertedMeas, out _))
                        {
                            effectiveValue = convertedMeas;
                        }
                        if (UnitConverter.TryToCanonical(t.Value, limitUnitEntity, canonicalUnitEntity,
                                canonical.MolarMass, out var limitCanonical, out _))
                        {
                            evaluatedLimitIds.Add(t.LimitId);
                            if (effectiveValue <= limitCanonical) continue;
                            var ratio = Math.Round(effectiveValue / limitCanonical, 4);
                            var valueLabel = m.NormalizedValue.HasValue
                                ? $"{effectiveValue:0.###} {canonicalUnitEntity.Symbol} (normalized)"
                                : $"{effectiveValue:0.###} {canonicalUnitEntity.Symbol}";
                            newEvents.Add(ComplianceEvent.ForLimitExceedance(
                                Guid.NewGuid(), t.EmissionSourceId,
                                measurementId: m.Id, t.LimitId, ratio, m.WindowStart, m.WindowEnd,
                                notes: $"{valueLabel} > limit {t.Value:0.###} {limitUnit.Symbol} " +
                                       $"(= {limitCanonical:0.###} {canonicalUnitEntity.Symbol}, ratio {ratio:0.##})"));
                            existingKeys.Add((t.LimitId, t.EmissionSourceId));
                            continue;
                        }
                    }

                    // Path 3 — concentration × volumetric flow → mass flow.
                    var derived = TryDeriveMassFlow(t, m.Value, limitUnit, measurementUnit, flowByKey, units);
                    if (derived is not null)
                    {
                        evaluatedLimitIds.Add(t.LimitId);
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

                    // Path 4 — nothing applies; surface to operator instead of silently skipping.
                    if (existingUnenforceableLimitIds.Add(t.LimitId))
                    {
                        newEvents.Add(ComplianceEvent.ForUnenforceableLimit(
                            Guid.NewGuid(), t.EmissionSourceId, t.LimitId, windowStart, windowEnd,
                            notes: BuildUnenforceableNote(t, limitUnit, canonicalUnitEntity, canonical)));
                    }
                }
            }
        }
        return (newEvents, evaluatedLimitIds);
    }

    private static Dictionary<Guid, MeasureUnit> BuildUnitEntities(IReadOnlyDictionary<Guid, UnitInfo> units) =>
        units.ToDictionary(
            kvp => kvp.Key,
            kvp => MeasureUnit.New(kvp.Key, kvp.Value.Symbol, kvp.Value.Dimension, kvp.Value.ToBaseFactor));

    private static string BuildUnenforceableNote(
        LimitTarget t, UnitInfo limitUnit, MeasureUnit canonicalUnit, PollutantCanonical canonical)
    {
        var molarTag = canonical.MolarMass.HasValue
            ? $"M={canonical.MolarMass:0.###} g/mol"
            : "no molar mass";
        return $"Limit {t.Value:0.###} {limitUnit.Symbol} ({limitUnit.Dimension}) cannot be reconciled " +
               $"with pollutant canonical {canonicalUnit.Symbol} ({canonicalUnit.Dimension}, {molarTag}); " +
               $"no volumetric flow available for derivation. Detection skipped.";
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
        IReadOnlyDictionary<Guid, MeasureUnit> unitEntities,
        IReadOnlyDictionary<Guid, PollutantCanonical> canonicals,
        IReadOnlyDictionary<Guid, FlowReading> flowByKey,
        HashSet<(Guid LimitId, Guid EmissionSourceId)> existingKeys,
        HashSet<Guid> existingUnenforceableLimitIds,
        HashSet<Guid> evaluatedLimitIds,
        DateTime fallbackWindowStart,
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

            if (contributingSources.Count == 0)
            {
                // Entire aggregate is unenforceable this tick — no source could be reconciled with
                // the limit's unit. Emit one UnenforceableLimit per LimitId so operator sees the
                // silent gap; auto-close will resolve it once any source becomes derivable again
                // (e.g. operator starts shipping volumetric flow on the affected sources).
                // Canonical/unit lookups go first so a missing pollutant config doesn't mutate
                // the dedup set without us actually emitting an event.
                if (canonicals.TryGetValue(primary.PollutantId, out var canonical)
                    && unitEntities.TryGetValue(canonical.CanonicalUnitId, out var canonicalUnit)
                    && existingUnenforceableLimitIds.Add(primary.LimitId))
                {
                    sink.Add(ComplianceEvent.ForUnenforceableLimit(
                        Guid.NewGuid(), primary.EmissionSourceId, primary.LimitId,
                        windowStart ?? fallbackWindowStart, windowEnd,
                        notes: $"Installation aggregate: {BuildUnenforceableNote(primary, limitUnit, canonicalUnit, canonical)}"));
                }
                continue;
            }

            // At least one source contributed → aggregate was evaluable; clear any stale
            // UnenforceableLimit via the auto-close signal.
            evaluatedLimitIds.Add(primary.LimitId);

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

    private async Task<(List<ComplianceEvent> Events, HashSet<Guid> EvaluatedLimitIds)>
        DetectAnnualLoadExceedancesAsync(CancellationToken ct, Guid? enterpriseId = null)
    {
        var targets = await queries.GetActiveLimitTargetsAsync([LimitType.AnnualLoad], ct, enterpriseId);
        if (targets.Count == 0) return ([], []);

        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.LimitExceedance, ct);
        var existingKeys = existing
            .Where(e => e.LimitId.HasValue)
            .Select(e => (e.LimitId!.Value, e.EmissionSourceId))
            .ToHashSet();
        var existingUnenforceableLimitIds = (await complianceEventQueries.GetOpenByTypeAsync(
                ComplianceEventType.UnenforceableLimit, ct))
            .Where(e => e.LimitId.HasValue)
            .Select(e => e.LimitId!.Value)
            .ToHashSet();

        var newEvents = new List<ComplianceEvent>();
        var evaluatedLimitIds = new HashSet<Guid>();
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
            // Raw counts let us distinguish "no data" (legitimate quiet) from "data exists but
            // rolling-query couldn't fold it" (silent unenforced limit — should surface as event).
            var rawCounts = await queries.GetRawMeasurementCountsAsync(sourceIds, pollutantIds, from, now, ct);
            if (rolling.Count == 0 && rawCounts.Count == 0) continue;

            var canonicals = await queries.GetPollutantCanonicalsAsync(pollutantIds, ct);
            var unitIds = byPeriod.Select(t => t.UnitId)
                .Concat(rolling.Values.Select(r => r.UnitId))
                .Concat(canonicals.Values.Select(c => c.CanonicalUnitId))
                .Distinct()
                .ToArray();
            var units = await queries.GetUnitsAsync(unitIds, ct);
            var unitEntities = BuildUnitEntities(units);

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
                foreach (var (uid, info) in extra)
                {
                    units.TryAdd(uid, info);
                    unitEntities.TryAdd(uid, MeasureUnit.New(uid, info.Symbol, info.Dimension, info.ToBaseFactor));
                }
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
                byPeriod.ToList(), rolling, units, unitEntities, canonicals,
                flowByKey, existingKeys, existingUnenforceableLimitIds, evaluatedLimitIds,
                pollutantO2Refs, o2Avgs, from, now, window, newEvents);

            foreach (var t in byPeriod)
            {
                if (aggregateLimitIds.Contains(t.LimitId)) continue;
                if (existingKeys.Contains((t.LimitId, t.EmissionSourceId)))
                {
                    evaluatedLimitIds.Add(t.LimitId);
                    continue;
                }
                if (!units.TryGetValue(t.UnitId, out var limitUnit)) continue;
                if (!canonicals.TryGetValue(t.PollutantId, out var canonical)) continue;
                if (!unitEntities.TryGetValue(t.UnitId, out var limitUnitEntity)
                    || !unitEntities.TryGetValue(canonical.CanonicalUnitId, out var canonicalUnitEntity)) continue;

                if (!rolling.TryGetValue((t.EmissionSourceId, t.PollutantId), out var r))
                {
                    // Rolling-query returned nothing for this tuple. Two cases:
                    //   a) no raw data at all in the window → legitimate quiet, skip silently.
                    //   b) raw data exists but every slice failed UnitConverter against the
                    //      pollutant's canonical → silent unenforced limit. Surface as event.
                    var rawCount = rawCounts.GetValueOrDefault((t.EmissionSourceId, t.PollutantId), 0);
                    if (rawCount > 0 && existingUnenforceableLimitIds.Add(t.LimitId))
                    {
                        newEvents.Add(ComplianceEvent.ForUnenforceableLimit(
                            Guid.NewGuid(), t.EmissionSourceId, t.LimitId, from, now,
                            notes: $"AnnualLoad: {rawCount} raw measurement(s) in window but none could " +
                                   $"be folded into pollutant canonical {canonicalUnitEntity.Symbol} " +
                                   $"({canonicalUnitEntity.Dimension}" +
                                   (canonical.MolarMass.HasValue
                                       ? $", M={canonical.MolarMass:0.###} g/mol"
                                       : ", no molar mass") +
                                   $"). Limit {t.Value:0.###} {limitUnit.Symbol} cannot be enforced."));
                    }
                    continue;
                }

                if (!units.TryGetValue(r.UnitId, out var measurementUnit)) continue;

                // O₂ normalization for Concentration AnnualLoad limits — applied on whichever
                // unit r.AvgRate ends up being interpreted in.
                var normalizationApplied = false;
                var effectiveRate = r.AvgRate;
                if (measurementUnit.Dimension == MeasureUnitDimension.MassConcentration)
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

                // Path 1 — same dimension on both sides (most common production case).
                if (measurementUnit.Dimension == limitUnit.Dimension)
                {
                    evaluatedLimitIds.Add(t.LimitId);
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
                    continue;
                }

                // Path 2 — cross-dimension via UnitConverter (e.g. ppm AnnualLoad limit vs
                // canonical mg/m³ rolling average, when pollutant has molar mass).
                if (measurementUnit.Dimension == canonicalUnitEntity.Dimension
                    && UnitConverter.TryToCanonical(t.Value, limitUnitEntity, canonicalUnitEntity,
                        canonical.MolarMass, out var limitCanonical, out _))
                {
                    evaluatedLimitIds.Add(t.LimitId);
                    if (effectiveRate <= limitCanonical) continue;
                    var ratio = Math.Round(effectiveRate / limitCanonical, 4);
                    var label = normalizationApplied
                        ? $"avg {effectiveRate:0.###} {canonicalUnitEntity.Symbol} (normalized)"
                        : $"avg {effectiveRate:0.###} {canonicalUnitEntity.Symbol}";
                    newEvents.Add(ComplianceEvent.ForLimitExceedance(
                        Guid.NewGuid(), t.EmissionSourceId,
                        measurementId: null, t.LimitId, ratio, from, now,
                        notes: $"AnnualLoad: {label} over last {window.TotalDays:0}d > " +
                               $"limit {t.Value:0.###} {limitUnit.Symbol} " +
                               $"(= {limitCanonical:0.###} {canonicalUnitEntity.Symbol}, " +
                               $"ratio {ratio:0.##}, {r.Samples} samples)"));
                    continue;
                }

                // Path 3 — MassConcentration × volumetric flow → MassFlow.
                var derived = TryDeriveMassFlow(t, r.AvgRate, limitUnit, measurementUnit, flowByKey, units);
                if (derived is not null)
                {
                    evaluatedLimitIds.Add(t.LimitId);
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

                // Path 4 — unenforceable.
                if (existingUnenforceableLimitIds.Add(t.LimitId))
                {
                    newEvents.Add(ComplianceEvent.ForUnenforceableLimit(
                        Guid.NewGuid(), t.EmissionSourceId, t.LimitId, from, now,
                        notes: $"AnnualLoad: {BuildUnenforceableNote(t, limitUnit, canonicalUnitEntity, canonical)}"));
                }
            }
        }
        return (newEvents, evaluatedLimitIds);
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
        IReadOnlyDictionary<Guid, MeasureUnit> unitEntities,
        IReadOnlyDictionary<Guid, PollutantCanonical> canonicals,
        IReadOnlyDictionary<Guid, FlowReading> flowByKey,
        HashSet<(Guid LimitId, Guid EmissionSourceId)> existingKeys,
        HashSet<Guid> existingUnenforceableLimitIds,
        HashSet<Guid> evaluatedLimitIds,
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
                    excludedCount++;
                    continue;
                }

                sumBase += derived.MassFlowKgPerH;
                totalSamples += r.Samples;
                contributingSources.Add(t.EmissionSourceId);
                derivedCount++;
            }

            if (contributingSources.Count == 0)
            {
                // Same operand-ordering discipline as the Fast aggregate: only add to the dedup
                // set when we're actually about to emit, never as a side-effect of a check that
                // might short-circuit.
                if (canonicals.TryGetValue(primary.PollutantId, out var canonical)
                    && unitEntities.TryGetValue(canonical.CanonicalUnitId, out var canonicalUnit)
                    && existingUnenforceableLimitIds.Add(primary.LimitId))
                {
                    sink.Add(ComplianceEvent.ForUnenforceableLimit(
                        Guid.NewGuid(), primary.EmissionSourceId, primary.LimitId, from, to,
                        notes: $"AnnualLoad installation aggregate: " +
                               $"{BuildUnenforceableNote(primary, limitUnit, canonicalUnit, canonical)}"));
                }
                continue;
            }

            evaluatedLimitIds.Add(primary.LimitId);

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

    private async Task<List<ComplianceEvent>> DetectDeviceOfflineAsync(
        CancellationToken ct, Guid? enterpriseId = null)
    {
        var now = DateTime.UtcNow;
        var threshold = TimeSpan.FromMinutes(_settings.DeviceOfflineThresholdMinutes);
        var cutoff = now - threshold;
        var graceLine = now - TimeSpan.FromDays(Math.Max(0, _settings.NewDeviceGraceDays));

        var devices = await queries.GetOperationalDevicesAsync(ct, enterpriseId);
        if (devices.Count == 0) return [];

        var existing = await complianceEventQueries.GetOpenByTypeAsync(
            ComplianceEventType.DeviceOffline, ct);
        var existingDeviceIds = existing
            .Where(e => e.DeviceId.HasValue)
            .Select(e => e.DeviceId!.Value)
            .ToHashSet();

        var lastSeen = await queries.GetDeviceLastSeenAsync(
            devices.Select(d => d.Id).ToArray(), cutoff, ct);

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

    private async Task<List<ComplianceEvent>> DetectCalibrationFailuresAsync(
        CancellationToken ct, Guid? enterpriseId = null)
    {
        var now = DateTime.UtcNow;
        var graceLine = now - TimeSpan.FromDays(Math.Max(0, _settings.NewDeviceGraceDays));

        var snapshots = await queries.GetDevicesWithLatestCalibrationAsync(ct, enterpriseId);
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

    private async Task<List<ComplianceEvent>> DetectDataAvailabilityLossAsync(
        CancellationToken ct, Guid? enterpriseId = null)
    {
        var targets = await queries.GetActiveLimitTargetsAsync(RateBasedLimits, ct, enterpriseId);
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

    private async Task<List<ComplianceEvent>> DetectMissingMeasurementAsync(
        CancellationToken ct, Guid? enterpriseId = null)
    {
        var window = TimeSpan.FromMinutes(_settings.MissingMeasurementWindowMinutes);
        var to = DateTime.UtcNow;
        var from = to - window;

        var targets = await queries.GetActiveLimitTargetsAsync(RateBasedLimits, ct, enterpriseId);
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

    private async Task<List<ComplianceEvent>> DetectOutOfRangeReadingsAsync(
        CancellationToken ct, Guid? enterpriseId = null)
    {
        var now = DateTime.UtcNow;
        var windowMinutes = Math.Max(1, _settings.OutOfRangeWindowMinutes);
        var from = now - TimeSpan.FromMinutes(windowMinutes);

        var windows = await queries.GetOutOfRangeWindowsAsync(
            from, now, _settings.OutOfRangeThreshold, _settings.OutOfRangeMinSampleCount, ct, enterpriseId);
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
