using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Settings;
using Domain.Entities.Monitoring;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Compliance;

/// <summary>
/// Builds Measurement aggregate records from measurement_1m for each closed window of every
/// (source, pollutant, period) tuple covered by an active limit. Applies IED Annex V Part 2 §7
/// normalization (O₂ + T + P + H₂O) when matching process parameters are available.
/// All DB reads go through IComplianceDetectionQueries; writes through IMeasurementRepository.
/// </summary>
public class MeasurementMaterializationService(
    IComplianceDetectionQueries queries,
    IMeasurementRepository measurementRepository,
    IUnitOfWork unitOfWork,
    IOptions<ComplianceDetectionSettings> options,
    ILogger<MeasurementMaterializationService> logger)
{
    private static readonly LimitType[] RateBasedLimits = [LimitType.Concentration, LimitType.MassFlow];
    private readonly ComplianceDetectionSettings _settings = options.Value;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var tuples = await queries.GetActiveMaterializationTuplesAsync(RateBasedLimits, cancellationToken);
        if (tuples.Count == 0)
        {
            logger.LogDebug("Materialization skipped: no active limits.");
            return;
        }

        var sourceIds = tuples.Select(t => t.SourceId).Distinct().ToArray();
        var deviceLookup = await queries.GetFirstDevicePerSourceAsync(sourceIds, cancellationToken);
        var o2RefByPollutant = await queries.GetPollutantO2ReferencesAsync(
            tuples.Select(t => t.PollutantId).Distinct().ToArray(), cancellationToken);

        var newMeasurements = new List<Measurement>();
        var now = DateTime.UtcNow;
        var fallbackEarliest = now - TimeSpan.FromDays(Math.Max(1, _settings.BackfillDays));

        foreach (var byPeriod in tuples.GroupBy(t => t.Period))
        {
            var period = PeriodToTimeSpan(byPeriod.Key);
            if (period == TimeSpan.Zero) continue;

            var groupTuples = byPeriod.ToArray();
            var groupSources = groupTuples.Select(t => t.SourceId).Distinct().ToArray();
            var groupPollutants = groupTuples.Select(t => t.PollutantId).Distinct().ToArray();

            var lastEnds = await queries.GetLastWindowEndsAsync(
                groupSources, groupPollutants, byPeriod.Key, cancellationToken);

            var earliest = lastEnds.Values
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .DefaultIfEmpty(fallbackEarliest)
                .Min();

            var lastClosedEnd = FloorToPeriod(now, period);
            if (earliest >= lastClosedEnd) continue;

            var buckets = await queries.GetReBucketedBulkAsync(
                groupSources, groupPollutants, period, earliest, lastClosedEnd, cancellationToken);
            if (buckets.Count == 0) continue;

            // Preload process-parameter averages per (source, window). Required only when a
            // pollutant in this group has Pollutant.DefaultO2Reference set (NOx, SO2, CO, …).
            var needsProcessParams = groupPollutants.Any(p =>
                o2RefByPollutant.GetValueOrDefault(p).HasValue);
            var paramReadings = needsProcessParams
                ? await queries.GetProcessParameterAveragesAsync(
                    groupSources, period, earliest, lastClosedEnd, cancellationToken)
                : new Dictionary<(Guid, DateTime), ProcessParamReadings>();

            foreach (var tuple in groupTuples)
            {
                if (!deviceLookup.TryGetValue(tuple.SourceId, out var deviceId)) continue;
                if (!buckets.TryGetValue((tuple.SourceId, tuple.PollutantId), out var tupleBuckets)) continue;

                var tupleLastEnd = lastEnds.GetValueOrDefault((tuple.SourceId, tuple.PollutantId));
                var o2Ref = o2RefByPollutant.GetValueOrDefault(tuple.PollutantId);
                var windowsThisTick = 0;

                foreach (var entry in tupleBuckets.OrderBy(b => b.WindowStart))
                {
                    if (windowsThisTick >= _settings.BackfillWindowsPerTick) break;
                    var windowStart = entry.WindowStart;
                    var windowEnd = windowStart + period;
                    if (windowEnd > lastClosedEnd) break;
                    if (tupleLastEnd.HasValue && windowEnd <= tupleLastEnd.Value) continue;

                    if (entry.SampleCount == 0 || entry.Avg is null) continue;

                    var expected = (int)period.TotalMinutes;
                    var normalizedValue = TryComputeNormalized(
                        entry.Avg.Value, o2Ref, tuple.SourceId, windowStart, paramReadings);

                    var measurement = Measurement.New(
                        Guid.NewGuid(),
                        windowStart, windowEnd,
                        tuple.Period, Aggregation.Average,
                        tuple.SourceId, tuple.PollutantId, deviceId, entry.UnitId,
                        value: entry.Avg.Value,
                        validPointsCount: (int)Math.Min(int.MaxValue, entry.ValidCount),
                        expectedPointsCount: expected,
                        normalizedValue: normalizedValue);

                    newMeasurements.Add(measurement);
                    windowsThisTick++;
                }
            }
        }

        if (newMeasurements.Count > 0)
        {
            await measurementRepository.AddRangeAsync(newMeasurements, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Materialized {Count} Measurement records in {Ms}ms",
            newMeasurements.Count, (DateTime.UtcNow - start).TotalMilliseconds);
    }

    /// <summary>
    /// IED Annex V Part 2 §7 normalization to standard conditions
    /// (273.15 K, 101.325 kPa, dry gas, reference O₂).
    /// </summary>
    /// <remarks>
    /// Formula: C_norm = C × (T_actual/T_ref) × (P_ref/P_actual)
    ///                 × 1/(1−H2O_fraction) × (21−O2_ref)/(21−O2_actual).
    /// Missing process parameters fall back to their reference value (correction factor = 1).
    /// O₂ correction is required when the pollutant has a DefaultO2Reference;
    /// if O₂ data is absent or in sensor-fault range, returns null.
    /// Assumes T input is °C, P input is kPa, H₂O input is percent — the project does not yet
    /// auto-convert between dimensional units for these.
    /// </remarks>
    private static decimal? TryComputeNormalized(
        decimal measuredValue,
        decimal? o2Reference,
        Guid sourceId,
        DateTime windowStart,
        IReadOnlyDictionary<(Guid, DateTime), ProcessParamReadings> paramReadings)
    {
        if (o2Reference is null) return null;
        if (!paramReadings.TryGetValue((sourceId, windowStart), out var p)) return null;
        if (p.O2Percent is null || p.O2Percent >= 21m || p.O2Percent < 0.5m) return null;

        const decimal tRefKelvin = 273.15m;
        const decimal pRefKPa = 101.325m;

        var normalized = measuredValue * (21m - o2Reference.Value) / (21m - p.O2Percent.Value);
        if (p.TemperatureCelsius is { } tC && tC > -273.15m)
        {
            normalized *= (tC + 273.15m) / tRefKelvin;
        }
        if (p.PressureKPa is { } pKPa && pKPa > 0m)
        {
            normalized *= pRefKPa / pKPa;
        }
        if (p.MoisturePercent is { } h2OPct && h2OPct >= 0m && h2OPct < 100m)
        {
            var moistureFraction = h2OPct / 100m;
            normalized *= 1m / (1m - moistureFraction);
        }
        return Math.Round(normalized, 6);
    }

    private static DateTime FloorToPeriod(DateTime t, TimeSpan period)
    {
        var ticks = t.Ticks - (t.Ticks % period.Ticks);
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private static TimeSpan PeriodToTimeSpan(AveragingWindow period) => period switch
    {
        AveragingWindow.Minute1 => TimeSpan.FromMinutes(1),
        AveragingWindow.Minute10 => TimeSpan.FromMinutes(10),
        AveragingWindow.HalfHour => TimeSpan.FromMinutes(30),
        AveragingWindow.Hour1 => TimeSpan.FromHours(1),
        AveragingWindow.Hour24 => TimeSpan.FromHours(24),
        _ => TimeSpan.Zero
    };
}
