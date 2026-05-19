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

internal class MeasurementRepository(ApplicationDbContext context)
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
