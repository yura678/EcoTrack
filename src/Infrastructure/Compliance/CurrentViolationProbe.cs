using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Settings;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
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

        var byPeriodLatest = new Dictionary<(Guid, Guid, AveragingWindow), MeasurementSnapshot>();
        var byPeriodRolling = new Dictionary<(Guid, Guid, AveragingWindow), RollingAverage>();
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
                var now = DateTime.UtcNow;
                var sources = pairsInPeriod.Select(p => p.SourceId).Distinct().ToArray();
                var pollutants = pairsInPeriod.Select(p => p.PollutantId).Distinct().ToArray();
                var rolling = await queries.GetRollingAverageRateAsync(
                    sources, pollutants, now - window, now, ct);
                foreach (var (key, r) in rolling)
                    byPeriodRolling[(key.SourceId, key.PollutantId, byPeriod.Key)] = r;
            }
            else
            {
                var snapshots = await queries.GetLatestMeasurementsAsync(pairsInPeriod, byPeriod.Key, ct);
                foreach (var s in snapshots)
                    byPeriodLatest[(s.EmissionSourceId, s.PollutantId, byPeriod.Key)] = s;
            }
        }

        var allUnitIds = limits.Values.Select(l => l.UnitId)
            .Concat(byPeriodLatest.Values.Select(s => s.UnitId))
            .Concat(byPeriodRolling.Values.Select(r => r.UnitId))
            .Distinct()
            .ToArray();
        var units = await queries.GetUnitsAsync(allUnitIds, ct);

        foreach (var e in eventsArr)
        {
            if (e.LimitId is null || !limits.TryGetValue(e.LimitId.Value, out var limit))
            {
                result[e.Id] = null;
                continue;
            }
            if (!units.TryGetValue(limit.UnitId, out var limitUnit))
            {
                result[e.Id] = null;
                continue;
            }

            var key = (e.EmissionSourceId, limit.PollutantId, limit.Period);
            if (IsAnnualLoadPeriod(limit.Period))
            {
                if (!byPeriodRolling.TryGetValue(key, out var rolling)
                    || !units.TryGetValue(rolling.UnitId, out var measurementUnit))
                {
                    result[e.Id] = null;
                    continue;
                }
                if (measurementUnit.Dimension != limitUnit.Dimension)
                {
                    result[e.Id] = null; // derived mass flow not probed in v1
                    continue;
                }
                var measured = rolling.AvgRate * measurementUnit.ToBaseFactor;
                var limitBase = limit.Value * limitUnit.ToBaseFactor;
                result[e.Id] = measured > limitBase;
            }
            else
            {
                if (!byPeriodLatest.TryGetValue(key, out var snapshot)
                    || !units.TryGetValue(snapshot.UnitId, out var measurementUnit))
                {
                    result[e.Id] = null;
                    continue;
                }
                if (snapshot.Quality != Quality.Valid && snapshot.Quality != Quality.Substituted)
                {
                    result[e.Id] = null;
                    continue;
                }
                if (measurementUnit.Dimension != limitUnit.Dimension)
                {
                    result[e.Id] = null;
                    continue;
                }
                var effective = snapshot.NormalizedValue ?? snapshot.Value;
                var measured = effective * measurementUnit.ToBaseFactor;
                var limitBase = limit.Value * limitUnit.ToBaseFactor;
                result[e.Id] = measured > limitBase;
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
        // Approximation: re-probe via the latest Measurement for the same (source, pollutant,
        // period) as the original Measurement referenced by the event. If we cannot recover the
        // tuple, return null. Probe ignores Quality and just checks availability fraction.
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

        var originals = await queries.GetMeasurementsByIdsAsync(measurementIds, ct);
        var byId = originals.ToDictionary(m => m.Id);

        var pairs = originals
            .GroupBy(o => o.Window)
            .ToDictionary(g => g.Key,
                g => g.Select(o => (o.EmissionSourceId, o.PollutantId)).Distinct().ToList());

        var latestByKey = new Dictionary<(Guid, Guid, AveragingWindow), MeasurementSnapshot>();
        foreach (var (period, list) in pairs)
        {
            var snapshots = await queries.GetLatestMeasurementsAsync(list, period, ct);
            foreach (var s in snapshots)
                latestByKey[(s.EmissionSourceId, s.PollutantId, period)] = s;
        }

        foreach (var e in eventsArr)
        {
            if (e.MeasurementId is null || !byId.TryGetValue(e.MeasurementId.Value, out var original))
            {
                result[e.Id] = null;
                continue;
            }
            var key = (original.EmissionSourceId, original.PollutantId, original.Window);
            if (!latestByKey.TryGetValue(key, out var latest) || latest.ExpectedPointsCount == 0)
            {
                result[e.Id] = null;
                continue;
            }
            var availability = (decimal)latest.ValidPointsCount / latest.ExpectedPointsCount;
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
