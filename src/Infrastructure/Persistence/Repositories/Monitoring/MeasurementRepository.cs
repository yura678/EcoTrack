using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Models;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Infrastructure.Compliance;
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
        var sources = await context.Set<EmissionSource>()
            .Where(s => s.InstallationId == installationId)
            .Select(s => new HeatmapSourceRow(
                s.Id, s.Code, s.InstallationId, s.Installation!.Name,
                s.Location.Y, s.Location.X))
            .ToListAsync(ct);
        if (sources.Count == 0) return [];

        return await BuildHeatmapPointsAsync(sources, pollutantId, ct);
    }

    public async Task<IReadOnlyList<ComplianceHeatmapPoint>> GetComplianceHeatmapBySiteAsync(
        Guid siteId, Guid pollutantId, CancellationToken ct)
    {
        var sources = await context.Set<EmissionSource>()
            .Where(s => s.Installation!.SiteId == siteId)
            .Select(s => new HeatmapSourceRow(
                s.Id, s.Code, s.InstallationId, s.Installation!.Name,
                s.Location.Y, s.Location.X))
            .ToListAsync(ct);
        if (sources.Count == 0) return [];

        return await BuildHeatmapPointsAsync(sources, pollutantId, ct);
    }

    private async Task<IReadOnlyList<ComplianceHeatmapPoint>> BuildHeatmapPointsAsync(
        IReadOnlyList<HeatmapSourceRow> sources, Guid pollutantId, CancellationToken ct)
    {
        var sourceIds = sources.Select(s => s.Id).ToArray();
        var installationIds = sources.Select(s => s.InstallationId).Distinct().ToArray();
        var sourcesByInstallation = sources
            .GroupBy(s => s.InstallationId)
            .ToDictionary(g => g.Key, g => g.Select(s => s.Id).ToArray());
        var now = DateTime.UtcNow;

        // Active limits for this pollutant:
        //   a) per-source — applies only to that source;
        //   b) installation-level Concentration — intensive, applies to every source of THAT
        //      installation (expanded below). For site scope this expansion is per-installation:
        //      a limit on installation A does NOT touch sources of installation B.
        // Installation-level MassFlow / AnnualLoad are extensive (need sum across sources) and
        // don't map to a single source colour, so they stay excluded; the aggregate endpoint
        // surfaces them separately.
        var activeLimits = await context.Set<EmissionLimit>()
            .Where(l =>
                l.PollutantId == pollutantId
                && (
                    (l.EmissionSourceId.HasValue && sourceIds.Contains(l.EmissionSourceId.Value))
                    || (l.InstallationId.HasValue
                        && installationIds.Contains(l.InstallationId.Value)
                        && l.LimitType == LimitType.Concentration)
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

        // Expand installation-level limits to their installation's sources only. A source that
        // has BOTH a per-source limit and an inherited installation-level limit will compete in
        // step "shortest period wins" below.
        var perSourceLimits = new List<LimitRow>(activeLimits.Count);
        foreach (var l in activeLimits)
        {
            if (l.SourceId.HasValue)
            {
                perSourceLimits.Add(l);
            }
            else if (l.InstallationId.HasValue
                     && sourcesByInstallation.TryGetValue(l.InstallationId.Value, out var sidsOfInst))
            {
                foreach (var sid in sidsOfInst)
                    perSourceLimits.Add(l with { SourceId = sid });
            }
        }

        // Shortest-period limit per source wins.
        var limitBySource = perSourceLimits
            .GroupBy(l => l.SourceId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(l => (int)l.Period).First());

        // Latest Measurement per (source, period). Two-step EF: scalar Max + IN filter.
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

        var openCounts = await context.Set<ComplianceEvent>()
            .Where(e => sourceIds.Contains(e.EmissionSourceId)
                        && e.Status == ComplianceEventStatus.Open)
            .GroupBy(e => e.EmissionSourceId)
            .Select(g => new { SourceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SourceId, x => x.Count, ct);

        // Sources without an active limit are still returned with severity=null — they show on
        // the map as "unmonitored" instead of disappearing.
        var result = new List<ComplianceHeatmapPoint>(sources.Count);
        foreach (var s in sources)
        {
            var openCount = openCounts.GetValueOrDefault(s.Id, 0);

            if (!limitBySource.TryGetValue(s.Id, out var lim))
            {
                result.Add(new ComplianceHeatmapPoint(
                    s.Id, s.Code, s.InstallationId, s.InstallationName,
                    s.Latitude, s.Longitude,
                    LimitId: null, LimitPeriod: null, LimitValue: null,
                    LimitUnitSymbol: null, CurrentValue: null,
                    CurrentValueIsNormalized: false, Severity: null,
                    OpenEventCount: openCount, MeasuredAt: null));
                continue;
            }

            if (!latestByKey.TryGetValue((s.Id, lim.Period), out var meas))
            {
                result.Add(new ComplianceHeatmapPoint(
                    s.Id, s.Code, s.InstallationId, s.InstallationName,
                    s.Latitude, s.Longitude,
                    lim.Id, lim.Period, lim.Value, lim.UnitSymbol,
                    CurrentValue: null, CurrentValueIsNormalized: false,
                    Severity: null, OpenEventCount: openCount, MeasuredAt: null));
                continue;
            }

            // Same-dimension only — cross-dimension severity needs derived flow which is out of
            // scope for the heatmap. Show the value without severity in that case.
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
                s.Id, s.Code, s.InstallationId, s.InstallationName,
                s.Latitude, s.Longitude,
                lim.Id, lim.Period, lim.Value, lim.UnitSymbol,
                CurrentValue: effective, CurrentValueIsNormalized: isNormalized,
                Severity: severity, OpenEventCount: openCount,
                MeasuredAt: meas.WindowEnd));
        }
        return result;
    }

    private record HeatmapSourceRow(
        Guid Id, string Code, Guid InstallationId, string InstallationName,
        double Latitude, double Longitude);

    public async Task<IReadOnlyList<ComplianceAggregatePoint>> GetComplianceAggregatesAsync(
        Guid installationId, Guid pollutantId, CancellationToken ct)
    {
        var installation = await context.Set<Installation>()
            .Where(i => i.Id == installationId)
            .Select(i => new { i.Id, i.Name })
            .FirstOrDefaultAsync(ct);
        if (installation is null) return [];

        return await BuildAggregatePointsAsync(
            new[] { (installation.Id, installation.Name) }, pollutantId, ct);
    }

    public async Task<IReadOnlyList<ComplianceAggregatePoint>> GetComplianceAggregatesBySiteAsync(
        Guid siteId, Guid pollutantId, CancellationToken ct)
    {
        var installations = await context.Set<Installation>()
            .Where(i => i.SiteId == siteId)
            .Select(i => new { i.Id, i.Name })
            .ToListAsync(ct);
        if (installations.Count == 0) return [];

        return await BuildAggregatePointsAsync(
            installations.Select(i => (i.Id, i.Name)).ToArray(), pollutantId, ct);
    }

    private async Task<IReadOnlyList<ComplianceAggregatePoint>> BuildAggregatePointsAsync(
        IReadOnlyCollection<(Guid Id, string Name)> installations,
        Guid pollutantId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var installationIds = installations.Select(i => i.Id).ToArray();
        var nameByInstallation = installations.ToDictionary(i => i.Id, i => i.Name);

        // Type-II installation-level limits across the requested installations. Concentration
        // installation-level limits stay out (they're intensive and surface via the per-source
        // heatmap).
        var limits = await context.Set<EmissionLimit>()
            .Where(l =>
                l.InstallationId.HasValue
                && installationIds.Contains(l.InstallationId.Value)
                && !l.EmissionSourceId.HasValue
                && l.PollutantId == pollutantId
                && (l.LimitType == LimitType.MassFlow || l.LimitType == LimitType.AnnualLoad)
                && l.ValidFrom <= now
                && (l.ValidTo == null || l.ValidTo >= now)
                && l.Permit!.PermitStatus == PermitStatus.Active
                && l.Permit!.ValidUntil >= now)
            .Select(l => new
            {
                Row = new AggregateLimitRow(
                    l.Id, l.LimitType, l.Period, l.Value,
                    l.Unit!.ToBaseFactor, l.Unit!.Symbol, l.Unit!.Dimension),
                InstallationId = l.InstallationId!.Value
            })
            .ToListAsync(ct);
        if (limits.Count == 0) return [];

        // Per-installation source lists. Sums never cross installation boundaries — a limit on
        // installation A only sees sources of installation A even when we run site-wide.
        var sourcesByInstallation = (await context.Set<EmissionSource>()
                .Where(s => installationIds.Contains(s.InstallationId))
                .Select(s => new { s.Id, s.InstallationId })
                .ToListAsync(ct))
            .GroupBy(s => s.InstallationId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToArray());

        var allLimitIds = limits.Select(x => x.Row.Id).ToList();
        var openByLimit = await context.Set<ComplianceEvent>()
            .Where(e => e.LimitId.HasValue
                        && allLimitIds.Contains(e.LimitId.Value)
                        && e.Status == ComplianceEventStatus.Open)
            .GroupBy(e => e.LimitId!.Value)
            .Select(g => new { LimitId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LimitId, x => x.Count, ct);

        var pollutantO2Refs = await detectionQueries.GetPollutantO2ReferencesAsync(
            new[] { pollutantId }, ct);

        var result = new List<ComplianceAggregatePoint>(limits.Count);
        foreach (var entry in limits)
        {
            if (!sourcesByInstallation.TryGetValue(entry.InstallationId, out var sources) ||
                sources.Length == 0) continue;

            var lim = entry.Row;
            var openCount = openByLimit.GetValueOrDefault(lim.Id, 0);
            var aggregate = lim.LimitType == LimitType.MassFlow
                ? await ComputeMassFlowAggregateAsync(lim, sources, pollutantId, now, ct)
                : await ComputeAnnualLoadAggregateAsync(lim, sources, pollutantId, now, pollutantO2Refs, ct);

            var severity = aggregate.AggregateBase is { } sumBase && lim.Value != 0m
                ? (decimal?)Math.Round(sumBase / (lim.Value * lim.UnitToBaseFactor), 4)
                : null;

            // Report aggregate in the limit's own unit — UI never needs to know base-unit math.
            var aggregateInLimitUnit = aggregate.AggregateBase is { } b && lim.UnitToBaseFactor != 0m
                ? (decimal?)Math.Round(b / lim.UnitToBaseFactor, 6)
                : null;

            result.Add(new ComplianceAggregatePoint(
                LimitId: lim.Id,
                InstallationId: entry.InstallationId,
                InstallationName: nameByInstallation[entry.InstallationId],
                LimitType: lim.LimitType,
                LimitPeriod: lim.Period,
                LimitValue: lim.Value,
                LimitUnitSymbol: lim.UnitSymbol,
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
            var derivedKgPerH = LimitComparisonHelpers.TryDeriveMassFlowKgPerH(
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

            var derivedKgPerH = LimitComparisonHelpers.TryDeriveMassFlowKgPerH(
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
        Guid? installationId,
        Guid? emissionSourceId,
        Guid? pollutantId,
        AveragingWindow? window,
        Quality? quality,
        DateTime? from,
        DateTime? to,
        MeasurementSort sort,
        SortDirection direction,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = BuildFilteredQuery(
            installationId, emissionSourceId, pollutantId, window, quality, from, to);

        var total = await query.CountAsync(cancellationToken);

        // Includes only for the page slice — keeps Count query trivial. The DTO needs
        // EmissionSource.Code, Pollutant.Code, Unit.Symbol to populate top-level convenience
        // fields without per-row navigation (previously a latent N+1).
        var withIncludes = query
            .Include(m => m.EmissionSource)
            .Include(m => m.Pollutant)
            .Include(m => m.Unit);

        var sorted = ApplySort(withIncludes, sort, direction);

        var items = await sorted
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

    public async Task<MeasurementSummary> GetSummaryAsync(
        Guid? installationId,
        Guid? emissionSourceId,
        Guid? pollutantId,
        AveragingWindow? window,
        Quality? quality,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var query = BuildFilteredQuery(
            installationId, emissionSourceId, pollutantId, window, quality, from, to);

        // `IsRepresentative` is a computed property on the entity (DataAvailability >= 0.75)
        // and cannot be translated to SQL. Use the raw column expression instead — guard
        // against ExpectedPointsCount == 0 to avoid divide-by-zero on the DB side.
        var counts = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Valid = g.Count(m => m.Quality == Quality.Valid),
                NonRep = g.Count(m =>
                    m.ExpectedPointsCount == 0
                    || (double)m.ValidPointsCount / m.ExpectedPointsCount < 0.75),
                MinValue = (decimal?)g.Min(m => m.Value),
                MaxValue = (decimal?)g.Max(m => m.Value),
                AvgValue = (decimal?)g.Average(m => m.Value),
                LatestWindowEnd = (DateTime?)g.Max(m => m.WindowEnd),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (counts is null)
        {
            return new MeasurementSummary(
                0, 0, 0,
                new Dictionary<Quality, int>(),
                null, null, null, null,
                null, null);
        }

        var qualityBreakdown = await query
            .GroupBy(m => m.Quality)
            .Select(g => new { Quality = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Quality, x => x.Count, cancellationToken);

        // Unit symbol is meaningful only when the filter pins a single pollutant — otherwise
        // mixing dimensions produces a misleading "unitless" aggregate.
        string? unitSymbol = null;
        if (pollutantId.HasValue && counts.Total > 0)
        {
            unitSymbol = await query
                .OrderByDescending(m => m.WindowEnd)
                .Select(m => m.Unit!.Symbol)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new MeasurementSummary(
            TotalRows: counts.Total,
            ValidRows: counts.Valid,
            NonRepresentativeRows: counts.NonRep,
            QualityBreakdown: qualityBreakdown,
            MinValue: counts.MinValue,
            AvgValue: counts.AvgValue.HasValue ? Math.Round(counts.AvgValue.Value, 6) : null,
            MaxValue: counts.MaxValue,
            P95Value: null, // P95 not implemented in this pass — needs raw SQL fragment.
            UnitSymbol: unitSymbol,
            LatestWindowEnd: counts.LatestWindowEnd);
    }

    private IQueryable<Measurement> BuildFilteredQuery(
        Guid? installationId,
        Guid? emissionSourceId,
        Guid? pollutantId,
        AveragingWindow? window,
        Quality? quality,
        DateTime? from,
        DateTime? to)
    {
        var query = base.TableNoTracking;
        if (installationId.HasValue)
            query = query.Where(m => m.EmissionSource!.InstallationId == installationId.Value);
        if (emissionSourceId.HasValue)
            query = query.Where(m => m.EmissionSourceId == emissionSourceId.Value);
        if (pollutantId.HasValue)
            query = query.Where(m => m.PollutantId == pollutantId.Value);
        if (window.HasValue)
            query = query.Where(m => m.Window == window.Value);
        if (quality.HasValue)
            query = query.Where(m => m.Quality == quality.Value);
        if (from.HasValue)
            query = query.Where(m => m.WindowEnd >= from.Value);
        if (to.HasValue)
            query = query.Where(m => m.WindowEnd <= to.Value);
        return query;
    }

    private static IQueryable<Measurement> ApplySort(
        IQueryable<Measurement> query, MeasurementSort sort, SortDirection direction)
    {
        bool desc = direction == SortDirection.Desc;
        return sort switch
        {
            MeasurementSort.Value => desc
                ? query.OrderByDescending(m => m.Value)
                : query.OrderBy(m => m.Value),
            // ValidPointsCount / ExpectedPointsCount is computed (not stored) on the entity,
            // but the underlying columns are available for SQL-side ratio sorting. Guard
            // against ExpectedPointsCount == 0 to avoid divide-by-zero in the database.
            MeasurementSort.DataAvailability => desc
                ? query.OrderByDescending(m => m.ExpectedPointsCount == 0
                    ? 0.0
                    : m.ValidPointsCount * 1.0 / m.ExpectedPointsCount)
                : query.OrderBy(m => m.ExpectedPointsCount == 0
                    ? 0.0
                    : m.ValidPointsCount * 1.0 / m.ExpectedPointsCount),
            MeasurementSort.Quality => desc
                ? query.OrderByDescending(m => m.Quality)
                : query.OrderBy(m => m.Quality),
            _ => desc
                ? query.OrderByDescending(m => m.WindowEnd)
                : query.OrderBy(m => m.WindowEnd),
        };
    }
}
