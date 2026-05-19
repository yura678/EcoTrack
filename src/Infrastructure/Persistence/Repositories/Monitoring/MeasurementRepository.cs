using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Models;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class MeasurementRepository(
    ApplicationDbContext context,
    IComplianceDetectionQueries detectionQueries)
    : BaseAsyncRepository<Measurement>(context), IMeasurementRepository, IMeasurementQueries
{
    public async Task AddRangeAsync(IEnumerable<Measurement> entities, CancellationToken cancellationToken)
    {
        await Entities.AddRangeAsync(entities, cancellationToken);
    }

    public async Task<IReadOnlyList<Measurement>> GetForRescanAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        AveragingWindow window,
        DateTime fromWindowStart,
        DateTime toWindowStart,
        CancellationToken cancellationToken)
    {
        if (sourceIds.Count == 0 || pollutantIds.Count == 0) return [];
        return await Entities
            .Where(m => sourceIds.Contains(m.EmissionSourceId)
                        && pollutantIds.Contains(m.PollutantId)
                        && m.Window == window
                        && m.Aggregation == Aggregation.Average
                        && m.WindowStart >= fromWindowStart
                        && m.WindowStart < toWindowStart)
            .ToListAsync(cancellationToken);
    }

    public async Task<Option<Measurement>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity ?? Option<Measurement>.None;
    }

    public async Task<Option<Measurement>> GetByTimeStamp(DateTime timestamp, Guid pollutantId,
        Guid emissionSourceId,
        CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.WindowEnd == timestamp
                                      && x.PollutantId == pollutantId
                                      && x.EmissionSourceId == emissionSourceId,
                cancellationToken);

        return entity ?? Option<Measurement>.None;
    }

    public async Task<IReadOnlyList<ComplianceHeatmapPoint>> GetComplianceHeatmapAsync(
        Guid installationId, Guid pollutantId, CancellationToken ct)
    {
        // 1. Sources of the installation with PostGIS location. Includes soft-delete-filtered.
        var sources = await context.Set<EmissionSource>()
            .Where(s => s.InstallationId == installationId)
            .Select(s => new
            {
                s.Id,
                s.Code,
                Latitude = s.Location.Y,
                Longitude = s.Location.X
            })
            .ToListAsync(ct);
        if (sources.Count == 0) return [];

        var sourceIds = sources.Select(s => s.Id).ToArray();
        var now = DateTime.UtcNow;

        // 2. Active limits for this pollutant. Two flavours pass the filter:
        //    a) per-source limits (EmissionSourceId set, applies only to that source);
        //    b) installation-level Concentration limits (InstallationId set) — these are
        //       intensive, every source under the installation is held to the same number,
        //       so we expand them per-source below.
        //    Installation-level MassFlow / AnnualLoad are extensive (need sum across sources)
        //    and don't map to a single source colour, so they stay excluded; a separate
        //    aggregate endpoint will surface them.
        var activeLimits = await context.Set<EmissionLimit>()
            .Where(l =>
                l.PollutantId == pollutantId
                && (
                    (l.EmissionSourceId.HasValue && sourceIds.Contains(l.EmissionSourceId.Value))
                    || (l.InstallationId == installationId && l.LimitType == LimitType.Concentration)
                )
                && l.LimitType != LimitType.AnnualLoad
                && l.ValidFrom <= now
                && (l.ValidTo == null || l.ValidTo >= now)
                && l.Permit!.PermitStatus == PermitStatus.Active
                && l.Permit!.ValidUntil >= now)
            .Select(l => new LimitRow(
                l.Id,
                l.EmissionSourceId,
                l.InstallationId,
                l.Period,
                l.Value,
                l.Unit!.ToBaseFactor,
                l.Unit!.Symbol,
                l.Unit!.Dimension))
            .ToListAsync(ct);

        // Expand installation-level limits into per-source rows so the rest of the pipeline
        // sees one uniform shape. A source that already has its own per-source limit AND an
        // installation-level limit will end up with both candidates — the shortest-period
        // pick in step 3 then decides which wins.
        var perSourceLimits = new List<LimitRow>(activeLimits.Count);
        foreach (var l in activeLimits)
        {
            if (l.SourceId.HasValue)
            {
                perSourceLimits.Add(l);
            }
            else
            {
                foreach (var sid in sourceIds)
                    perSourceLimits.Add(l with { SourceId = sid });
            }
        }

        // 3. Pick the shortest-period limit per source (smallest enum value).
        var limitBySource = perSourceLimits
            .GroupBy(l => l.SourceId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(l => (int)l.Period).First());

        // 4. Latest Measurement per (source, period) for the selected limit pairs. Two-step
        // EF pattern: scalar Max + IN filter — same as IComplianceDetectionQueries does.
        Dictionary<(Guid SourceId, AveragingWindow Window), MeasurementRow> latestByKey = new();
        if (limitBySource.Count > 0)
        {
            var keyTuples = limitBySource
                .Select(kvp => (SourceId: kvp.Key, Period: kvp.Value.Period))
                .ToHashSet();
            var sourcesWithLimit = keyTuples.Select(k => k.SourceId).Distinct().ToArray();
            var periodsWithLimit = keyTuples.Select(k => k.Period).Distinct().ToArray();

            var maxEnds = await context.Set<Measurement>()
                .Where(m => sourcesWithLimit.Contains(m.EmissionSourceId)
                            && m.PollutantId == pollutantId
                            && periodsWithLimit.Contains(m.Window)
                            && m.Aggregation == Aggregation.Average)
                .GroupBy(m => new { m.EmissionSourceId, m.Window })
                .Select(g => new
                {
                    g.Key.EmissionSourceId,
                    g.Key.Window,
                    MaxEnd = g.Max(m => m.WindowEnd)
                })
                .ToListAsync(ct);

            var maxByKey = maxEnds
                .Where(x => keyTuples.Contains((x.EmissionSourceId, x.Window)))
                .ToDictionary(x => (x.EmissionSourceId, x.Window), x => x.MaxEnd);

            if (maxByKey.Count > 0)
            {
                var maxEndValues = maxByKey.Values.Distinct().ToArray();
                var rows = await context.Set<Measurement>()
                    .Where(m => sourcesWithLimit.Contains(m.EmissionSourceId)
                                && m.PollutantId == pollutantId
                                && periodsWithLimit.Contains(m.Window)
                                && m.Aggregation == Aggregation.Average
                                && maxEndValues.Contains(m.WindowEnd))
                    .Select(m => new MeasurementRow(
                        m.EmissionSourceId,
                        m.Window,
                        m.WindowEnd,
                        m.Value,
                        m.NormalizedValue,
                        m.Unit!.ToBaseFactor,
                        m.Unit!.Dimension))
                    .ToListAsync(ct);

                latestByKey = rows
                    .Where(r => maxByKey.TryGetValue((r.SourceId, r.Window), out var max)
                                && r.WindowEnd == max)
                    .ToDictionary(r => (r.SourceId, r.Window));
            }
        }

        // 5. Open ComplianceEvent count per source.
        var openCounts = await context.Set<ComplianceEvent>()
            .Where(e => sourceIds.Contains(e.EmissionSourceId)
                        && e.Status == ComplianceEventStatus.Open)
            .GroupBy(e => e.EmissionSourceId)
            .Select(g => new { SourceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SourceId, x => x.Count, ct);

        // 6. Combine. Sources without an active limit are still returned with severity=null —
        // they show on the map as "unmonitored" instead of disappearing.
        var result = new List<ComplianceHeatmapPoint>(sources.Count);
        foreach (var s in sources)
        {
            var openCount = openCounts.GetValueOrDefault(s.Id, 0);

            if (!limitBySource.TryGetValue(s.Id, out var lim))
            {
                result.Add(new ComplianceHeatmapPoint(
                    s.Id, s.Code, s.Latitude, s.Longitude,
                    LimitId: null, LimitPeriod: null, LimitValue: null,
                    LimitUnitSymbol: null, CurrentValue: null,
                    CurrentValueIsNormalized: false, Severity: null,
                    OpenEventCount: openCount, MeasuredAt: null));
                continue;
            }

            if (!latestByKey.TryGetValue((s.Id, lim.Period), out var meas))
            {
                result.Add(new ComplianceHeatmapPoint(
                    s.Id, s.Code, s.Latitude, s.Longitude,
                    lim.Id, lim.Period, lim.Value, lim.UnitSymbol,
                    CurrentValue: null, CurrentValueIsNormalized: false,
                    Severity: null, OpenEventCount: openCount, MeasuredAt: null));
                continue;
            }

            // Same-dimension only. Cross-dimension (e.g. concentration vs mass-flow limit)
            // requires derived flow — out of scope for the heatmap simplification; show
            // value without severity in that case.
            decimal? severity = null;
            var isNormalized = meas.NormalizedValue.HasValue;
            var effective = meas.NormalizedValue ?? meas.Value;
            if (meas.UnitDimension == lim.UnitDimension)
            {
                var valueBase = effective * meas.UnitToBaseFactor;
                var limitBase = lim.Value * lim.UnitToBaseFactor;
                if (limitBase != 0m)
                {
                    severity = Math.Round(valueBase / limitBase, 4);
                }
            }

            result.Add(new ComplianceHeatmapPoint(
                s.Id, s.Code, s.Latitude, s.Longitude,
                lim.Id, lim.Period, lim.Value, lim.UnitSymbol,
                CurrentValue: effective, CurrentValueIsNormalized: isNormalized,
                Severity: severity, OpenEventCount: openCount,
                MeasuredAt: meas.WindowEnd));
        }
        return result;
    }

    public async Task<IReadOnlyList<ComplianceAggregatePoint>> GetComplianceAggregatesAsync(
        Guid installationId, Guid pollutantId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // 1. Active installation-level limits of type MassFlow / AnnualLoad. Concentration
        // installation-level limits are intensive and already covered by the per-source
        // heatmap endpoint, so they're excluded here.
        var limits = await context.Set<EmissionLimit>()
            .Where(l =>
                l.InstallationId == installationId
                && !l.EmissionSourceId.HasValue
                && l.PollutantId == pollutantId
                && (l.LimitType == LimitType.MassFlow || l.LimitType == LimitType.AnnualLoad)
                && l.ValidFrom <= now
                && (l.ValidTo == null || l.ValidTo >= now)
                && l.Permit!.PermitStatus == PermitStatus.Active
                && l.Permit!.ValidUntil >= now)
            .Select(l => new AggregateLimitRow(
                l.Id, l.LimitType, l.Period, l.Value,
                l.Unit!.ToBaseFactor, l.Unit!.Symbol, l.Unit!.Dimension))
            .ToListAsync(ct);
        if (limits.Count == 0) return [];

        // 2. Sources of installation.
        var sourceIds = await context.Set<EmissionSource>()
            .Where(s => s.InstallationId == installationId)
            .Select(s => s.Id)
            .ToListAsync(ct);
        if (sourceIds.Count == 0) return [];
        var sourceIdsArr = sourceIds.ToArray();
        var pollutantIdsArr = new[] { pollutantId };

        // 3. Open events grouped by LimitId.
        var openByLimit = await context.Set<ComplianceEvent>()
            .Where(e => e.LimitId.HasValue
                        && limits.Select(l => l.Id).Contains(e.LimitId.Value)
                        && e.Status == ComplianceEventStatus.Open)
            .GroupBy(e => e.LimitId!.Value)
            .Select(g => new { LimitId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LimitId, x => x.Count, ct);

        // 4. Pollutant O2 reference (cached once — applies across AnnualLoad limits).
        var pollutantO2Refs = await detectionQueries.GetPollutantO2ReferencesAsync(
            pollutantIdsArr, ct);

        var result = new List<ComplianceAggregatePoint>(limits.Count);
        foreach (var lim in limits)
        {
            var openCount = openByLimit.GetValueOrDefault(lim.Id, 0);
            var aggregate = lim.LimitType == LimitType.MassFlow
                ? await ComputeMassFlowAggregateAsync(lim, sourceIdsArr, pollutantId, now, ct)
                : await ComputeAnnualLoadAggregateAsync(lim, sourceIdsArr, pollutantId, now, pollutantO2Refs, ct);

            var severity = aggregate.AggregateBase is { } sumBase && lim.Value != 0m
                ? (decimal?)Math.Round(sumBase / (lim.Value * lim.UnitToBaseFactor), 4)
                : null;

            // Report aggregate in the limit's own unit so the UI doesn't have to know about
            // base-unit conventions. Convert back from base by dividing by ToBaseFactor.
            var aggregateInLimitUnit = aggregate.AggregateBase is { } b && lim.UnitToBaseFactor != 0m
                ? (decimal?)Math.Round(b / lim.UnitToBaseFactor, 6)
                : null;

            result.Add(new ComplianceAggregatePoint(
                lim.Id, lim.LimitType, lim.Period, lim.Value, lim.UnitSymbol,
                AggregateValue: aggregateInLimitUnit,
                Severity: severity,
                ContributingSourcesCount: aggregate.Contributing,
                DerivedSourcesCount: aggregate.Derived,
                ExcludedSourcesCount: aggregate.Excluded,
                OpenEventCount: openCount,
                MeasuredAt: aggregate.MeasuredAt));
        }

        return result;
    }

    private record AggregateRunResult(
        decimal? AggregateBase,
        int Contributing,
        int Derived,
        int Excluded,
        DateTime? MeasuredAt);

    private async Task<AggregateRunResult> ComputeMassFlowAggregateAsync(
        AggregateLimitRow lim, Guid[] sourceIds, Guid pollutantId,
        DateTime now, CancellationToken ct)
    {
        // Same approach as ProcessInstallationAggregates: read the latest closed window for
        // this limit's period across all sources, sum same-dim measurements directly and
        // derive cross-dim ones via volumetric flow.
        var (windowFrom, windowEnd) = ComputeLastCompletedWindow(lim.Period, now);
        if (windowEnd == default)
            return new AggregateRunResult(null, 0, 0, 0, null);

        var measurements = await detectionQueries.GetMeasurementsForWindowAsync(
            sourceIds, new[] { pollutantId }, lim.Period, windowEnd, ct);
        if (measurements.Count == 0)
            return new AggregateRunResult(null, 0, 0, 0, null);

        var unitIds = measurements.Select(m => m.UnitId).Distinct().ToList();
        var units = await detectionQueries.GetUnitsAsync(unitIds, ct);

        // Pre-fetch volumetric flow only if any source actually needs derivation.
        var needsFlow = measurements.Any(m =>
            units.TryGetValue(m.UnitId, out var u)
            && u.Dimension == MeasureUnitDimension.MassConcentration
            && lim.UnitDimension == MeasureUnitDimension.MassFlow);
        var flowByKey = needsFlow
            ? await detectionQueries.GetVolumetricFlowForRangeAsync(
                sourceIds, windowFrom, windowEnd, ct)
            : new Dictionary<Guid, FlowReading>();
        if (flowByKey.Count > 0)
        {
            var extra = await detectionQueries.GetUnitsAsync(
                flowByKey.Values.Select(v => v.UnitId).Distinct().ToList(), ct);
            foreach (var (uid, info) in extra) units.TryAdd(uid, info);
        }

        decimal sumBase = 0m;
        var contributing = 0;
        var derived = 0;
        var excluded = 0;
        DateTime? measuredAt = null;

        foreach (var m in measurements)
        {
            if (m.Quality != Quality.Valid && m.Quality != Quality.Substituted)
            {
                excluded++;
                continue;
            }
            if (!units.TryGetValue(m.UnitId, out var measurementUnit))
            {
                excluded++;
                continue;
            }
            measuredAt = m.WindowEnd;

            if (measurementUnit.Dimension == lim.UnitDimension)
            {
                var effective = m.NormalizedValue ?? m.Value;
                sumBase += effective * measurementUnit.ToBaseFactor;
                contributing++;
                continue;
            }

            // Cross-dim: try MassConcentration × VolumetricFlow → kg/h derivation.
            var derivedKgPerH = TryDeriveMassFlowKgPerH(
                m.Value, measurementUnit, lim.UnitDimension, m.EmissionSourceId, flowByKey, units);
            if (derivedKgPerH is null)
            {
                excluded++;
                continue;
            }

            sumBase += derivedKgPerH.Value;
            contributing++;
            derived++;
        }

        return new AggregateRunResult(
            AggregateBase: contributing > 0 ? sumBase : null,
            Contributing: contributing,
            Derived: derived,
            Excluded: excluded,
            MeasuredAt: measuredAt);
    }

    private async Task<AggregateRunResult> ComputeAnnualLoadAggregateAsync(
        AggregateLimitRow lim, Guid[] sourceIds, Guid pollutantId,
        DateTime now, IReadOnlyDictionary<Guid, decimal?> pollutantO2Refs,
        CancellationToken ct)
    {
        var window = lim.Period switch
        {
            AveragingWindow.Month1 => TimeSpan.FromDays(30),
            AveragingWindow.Year1 => TimeSpan.FromDays(365),
            _ => TimeSpan.Zero
        };
        if (window == TimeSpan.Zero)
            return new AggregateRunResult(null, 0, 0, 0, null);

        var from = now - window;
        var rolling = await detectionQueries.GetRollingAverageRateAsync(
            sourceIds, new[] { pollutantId }, from, now, ct);
        if (rolling.Count == 0)
            return new AggregateRunResult(null, 0, 0, 0, null);

        var unitIds = rolling.Values.Select(r => r.UnitId).Distinct().ToList();
        var units = await detectionQueries.GetUnitsAsync(unitIds, ct);

        var needsFlow = lim.UnitDimension == MeasureUnitDimension.MassFlow
            && rolling.Values.Any(r =>
                units.TryGetValue(r.UnitId, out var u)
                && u.Dimension == MeasureUnitDimension.MassConcentration);
        var flowByKey = needsFlow
            ? await detectionQueries.GetVolumetricFlowForRangeAsync(sourceIds, from, now, ct)
            : new Dictionary<Guid, FlowReading>();
        if (flowByKey.Count > 0)
        {
            var extra = await detectionQueries.GetUnitsAsync(
                flowByKey.Values.Select(v => v.UnitId).Distinct().ToList(), ct);
            foreach (var (uid, info) in extra) units.TryAdd(uid, info);
        }

        // O2 normalization needed only for Concentration AnnualLoad limits — the detector
        // mirrors this restriction.
        var needsO2 = lim.UnitDimension == MeasureUnitDimension.MassConcentration
                      && pollutantO2Refs.GetValueOrDefault(pollutantId).HasValue;
        var o2Avgs = needsO2
            ? await detectionQueries.GetO2AverageForRangeAsync(sourceIds, from, now, ct)
            : new Dictionary<Guid, decimal>();

        decimal sumBase = 0m;
        var contributing = 0;
        var derived = 0;
        var excluded = 0;

        foreach (var (key, r) in rolling)
        {
            if (!units.TryGetValue(r.UnitId, out var measurementUnit))
            {
                excluded++;
                continue;
            }

            if (measurementUnit.Dimension == lim.UnitDimension)
            {
                var rate = r.AvgRate;
                if (lim.UnitDimension == MeasureUnitDimension.MassConcentration)
                {
                    var normalized = TryComputeAnnualO2Normalization(
                        rate, pollutantO2Refs.GetValueOrDefault(pollutantId),
                        o2Avgs.GetValueOrDefault(key.SourceId));
                    if (normalized.HasValue) rate = normalized.Value;
                }
                sumBase += rate * measurementUnit.ToBaseFactor;
                contributing++;
                continue;
            }

            var derivedKgPerH = TryDeriveMassFlowKgPerH(
                r.AvgRate, measurementUnit, lim.UnitDimension, key.SourceId, flowByKey, units);
            if (derivedKgPerH is null)
            {
                excluded++;
                continue;
            }

            sumBase += derivedKgPerH.Value;
            contributing++;
            derived++;
        }

        return new AggregateRunResult(
            AggregateBase: contributing > 0 ? sumBase : null,
            Contributing: contributing,
            Derived: derived,
            Excluded: excluded,
            MeasuredAt: contributing > 0 ? now : null);
    }

    private static decimal? TryDeriveMassFlowKgPerH(
        decimal measurementValue,
        UnitInfo measurementUnit,
        MeasureUnitDimension limitDimension,
        Guid sourceId,
        IReadOnlyDictionary<Guid, FlowReading> flowByKey,
        IReadOnlyDictionary<Guid, UnitInfo> units)
    {
        if (limitDimension != MeasureUnitDimension.MassFlow) return null;
        if (measurementUnit.Dimension != MeasureUnitDimension.MassConcentration) return null;
        if (!flowByKey.TryGetValue(sourceId, out var flow)) return null;
        if (!units.TryGetValue(flow.UnitId, out var flowUnit)) return null;
        if (flowUnit.Dimension != MeasureUnitDimension.VolumetricFlow) return null;

        var concBase = measurementValue * measurementUnit.ToBaseFactor; // mg/m³
        var flowBase = flow.Value * flowUnit.ToBaseFactor;               // m³/h
        return Math.Round((concBase * flowBase) / 1_000_000m, 6);        // mg/h → kg/h
    }

    private static decimal? TryComputeAnnualO2Normalization(
        decimal rawAvgRate, decimal? o2Reference, decimal o2Actual)
    {
        if (o2Reference is null) return null;
        if (o2Actual >= 21m || o2Actual < 0.5m) return null;
        if (o2Actual == 0m) return null;
        return Math.Round(rawAvgRate * (21m - o2Reference.Value) / (21m - o2Actual), 6);
    }

    private static (DateTime From, DateTime End) ComputeLastCompletedWindow(
        AveragingWindow period, DateTime now)
    {
        var ts = period switch
        {
            AveragingWindow.Minute1 => TimeSpan.FromMinutes(1),
            AveragingWindow.Minute10 => TimeSpan.FromMinutes(10),
            AveragingWindow.HalfHour => TimeSpan.FromMinutes(30),
            AveragingWindow.Hour1 => TimeSpan.FromHours(1),
            AveragingWindow.Hour24 => TimeSpan.FromHours(24),
            _ => TimeSpan.Zero
        };
        if (ts == TimeSpan.Zero) return (default, default);

        var floored = new DateTime(now.Ticks - (now.Ticks % ts.Ticks), DateTimeKind.Utc);
        return (floored - ts, floored);
    }

    private record AggregateLimitRow(
        Guid Id,
        LimitType LimitType,
        AveragingWindow Period,
        decimal Value,
        decimal UnitToBaseFactor,
        string UnitSymbol,
        MeasureUnitDimension UnitDimension);

    private record MeasurementRow(
        Guid SourceId,
        AveragingWindow Window,
        DateTime WindowEnd,
        decimal Value,
        decimal? NormalizedValue,
        decimal UnitToBaseFactor,
        MeasureUnitDimension UnitDimension);

    /// <summary>
    /// EF projection of a limit candidate. SourceId is nullable because installation-level
    /// limits arrive without a single source anchor and get expanded later.
    /// </summary>
    private record LimitRow(
        Guid Id,
        Guid? SourceId,
        Guid? InstallationId,
        AveragingWindow Period,
        decimal Value,
        decimal UnitToBaseFactor,
        string UnitSymbol,
        MeasureUnitDimension UnitDimension);

    public async Task<PageResult<Measurement>> GetPagedAsync(
        Guid installationId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = base.TableNoTracking
            .Where(x => x.EmissionSource!.InstallationId == installationId);

        if (from.HasValue)
        {
            query = query.Where(x => x.WindowEnd >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.WindowEnd <= to.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.WindowEnd)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<Measurement>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
