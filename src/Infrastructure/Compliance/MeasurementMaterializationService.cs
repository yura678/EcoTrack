using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Settings;
using Domain.Entities.Monitoring;
using Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Compliance;

/// <summary>
/// Builds Measurement aggregate records from measurement_1m for each closed window of every
/// (source, pollutant, period) tuple covered by an active limit. Applies IED Annex V Part 2 §7
/// normalization (O₂ + T + P + H₂O) when matching process parameters are available.
///
/// Phase 2 of the canonical-unit refactor: measurement_1m now keeps unit_id in GROUP BY, so a
/// window can deliver multiple per-unit AggregateBucket rows. The materializer converts each row
/// into the pollutant's CanonicalUnit via <see cref="UnitConverter"/> and weighted-averages them
/// (weight = sample_count) before persisting one Measurement per (source, pollutant, window) with
/// Measurement.UnitId = pollutant.CanonicalUnitId.
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

    /// <summary>
    /// Legacy entry point — materialises every active tuple across all tenants in one pass.
    /// Kept for the IHostedService runner during the Hangfire migration; once Phase F deletes
    /// the hosted service, this overload becomes dead code.
    /// </summary>
    public Task RunAsync(CancellationToken cancellationToken) =>
        RunInternalAsync(enterpriseId: null, cancellationToken);

    /// <summary>
    /// Per-tenant entry point — used by the Hangfire fan-out so a slow tenant only blocks its
    /// own worker. Behaviour is identical to <see cref="RunAsync"/> except that every entry
    /// query is scoped to the given enterprise.
    /// </summary>
    public Task RunForEnterpriseAsync(Guid enterpriseId, CancellationToken cancellationToken) =>
        RunInternalAsync(enterpriseId, cancellationToken);

    private async Task RunInternalAsync(Guid? enterpriseId, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var tuples = await queries.GetActiveMaterializationTuplesAsync(
            RateBasedLimits, cancellationToken, enterpriseId);
        if (tuples.Count == 0)
        {
            logger.LogDebug(
                "Materialization skipped: no active limits (enterprise: {Enterprise}).",
                enterpriseId?.ToString() ?? "all");
            return;
        }

        var pollutantIds = tuples.Select(t => t.PollutantId).Distinct().ToArray();
        var pollutantCanonicals = await queries.GetPollutantCanonicalsAsync(pollutantIds, cancellationToken);
        var o2RefByPollutant = await queries.GetPollutantO2ReferencesAsync(pollutantIds, cancellationToken);

        var newMeasurements = new List<Measurement>();
        var updatedCount = 0;
        var now = DateTime.UtcNow;
        var backfillCap = now - TimeSpan.FromDays(Math.Max(1, _settings.MaxBackfillDays));
        var rescanWindows = Math.Max(0, _settings.LateArrivingRescanWindows);

        foreach (var byPeriod in tuples.GroupBy(t => t.Period))
        {
            var period = PeriodToTimeSpan(byPeriod.Key);
            if (period == TimeSpan.Zero) continue;

            var groupTuples = byPeriod.ToArray();
            var groupSources = groupTuples.Select(t => t.SourceId).Distinct().ToArray();
            var groupPollutants = groupTuples.Select(t => t.PollutantId).Distinct().ToArray();

            var lastEnds = await queries.GetLastWindowEndsAsync(
                groupSources, groupPollutants, byPeriod.Key, cancellationToken);

            var lastClosedEnd = FloorToPeriod(now, period);
            var rescanSpan = TimeSpan.FromTicks(period.Ticks * rescanWindows);

            // Per-tuple start: tuples with history are extended backwards by rescanSpan so
            // late-arriving raw data in already-materialized windows is picked up. Tuples
            // without history backfill from max(limit.ValidFrom, now − MaxBackfillDays).
            var perTupleStart = new Dictionary<(Guid SourceId, Guid PollutantId), DateTime>();
            foreach (var tuple in groupTuples)
            {
                var key = (tuple.SourceId, tuple.PollutantId);
                var lastEnd = lastEnds.GetValueOrDefault(key);
                DateTime tupleStart;
                if (lastEnd.HasValue)
                {
                    tupleStart = lastEnd.Value - rescanSpan;
                }
                else
                {
                    var floor = tuple.EarliestValidFrom > backfillCap
                        ? tuple.EarliestValidFrom
                        : backfillCap;
                    tupleStart = FloorToPeriod(floor, period);
                }
                perTupleStart[key] = tupleStart;
            }

            var earliest = perTupleStart.Values.DefaultIfEmpty(lastClosedEnd).Min();
            if (earliest >= lastClosedEnd) continue;

            var buckets = await queries.GetReBucketedBulkAsync(
                groupSources, groupPollutants, period, earliest, lastClosedEnd, cancellationToken);
            if (buckets.Count == 0) continue;

            // Load every unit involved in this period's buckets plus each pollutant's canonical
            // unit. Pulled together so UnitConverter has everything it needs in-memory.
            var unitIds = buckets.Values
                .SelectMany(rows => rows.Select(r => r.UnitId))
                .Concat(pollutantCanonicals.Values.Select(c => c.CanonicalUnitId))
                .Distinct()
                .ToArray();
            var unitInfos = await queries.GetUnitsAsync(unitIds, cancellationToken);
            // Shadow MeasureUnit entities preserve Id so UnitConverter's identity short-circuit
            // works; the conversion is pure on (Id, Dimension, ToBaseFactor, Symbol).
            var unitEntities = unitInfos.ToDictionary(
                kvp => kvp.Key,
                kvp => MeasureUnit.New(kvp.Key, kvp.Value.Symbol, kvp.Value.Dimension, kvp.Value.ToBaseFactor));

            // Preload existing Measurement entities in the rescan span (tracked) so late-data
            // updates mutate in place rather than violating the unique (source,pollutant,end)
            // index. Pre-existing records outside rescanSpan are not touched.
            var existing = await measurementRepository.GetForRescanAsync(
                groupSources, groupPollutants, byPeriod.Key,
                fromWindowStart: earliest, toWindowStart: lastClosedEnd, cancellationToken);
            var existingByKey = existing.ToDictionary(
                m => (m.EmissionSourceId, m.PollutantId, m.WindowStart));

            var needsProcessParams = groupPollutants.Any(p =>
                o2RefByPollutant.GetValueOrDefault(p).HasValue);
            var paramReadings = needsProcessParams
                ? await queries.GetProcessParameterAveragesAsync(
                    groupSources, period, earliest, lastClosedEnd, cancellationToken)
                : new Dictionary<(Guid, DateTime), ProcessParamReadings>();

            foreach (var tuple in groupTuples)
            {
                if (!buckets.TryGetValue((tuple.SourceId, tuple.PollutantId), out var tupleBuckets)) continue;
                if (!pollutantCanonicals.TryGetValue(tuple.PollutantId, out var canonical))
                {
                    logger.LogWarning(
                        "Pollutant {PollutantId} has no canonical-unit info; skipping materialization for this tick.",
                        tuple.PollutantId);
                    continue;
                }
                if (!unitEntities.TryGetValue(canonical.CanonicalUnitId, out var canonicalUnit))
                {
                    logger.LogWarning(
                        "Canonical unit {UnitId} for pollutant {PollutantId} could not be loaded; skipping.",
                        canonical.CanonicalUnitId, tuple.PollutantId);
                    continue;
                }

                var tupleStart = perTupleStart[(tuple.SourceId, tuple.PollutantId)];
                var o2Ref = o2RefByPollutant.GetValueOrDefault(tuple.PollutantId);
                var insertedThisTick = 0;

                // Fan rows back together per WindowStart — each window may contain multiple
                // per-unit slices that need converting to canonical before averaging.
                var byWindow = tupleBuckets
                    .GroupBy(b => b.WindowStart)
                    .OrderBy(g => g.Key);

                foreach (var windowGroup in byWindow)
                {
                    var windowStart = windowGroup.Key;
                    var windowEnd = windowStart + period;
                    if (windowEnd > lastClosedEnd) break;
                    if (windowEnd <= tupleStart) continue;

                    decimal weightedSum = 0m;
                    long totalSampleCount = 0;
                    long validMinutes = 0;
                    foreach (var entry in windowGroup)
                    {
                        validMinutes = Math.Max(validMinutes, entry.ValidMinutesInWindow);
                        if (entry.SampleCount == 0 || entry.Avg is null) continue;
                        if (!unitEntities.TryGetValue(entry.UnitId, out var fromUnit))
                        {
                            logger.LogWarning(
                                "Raw unit {UnitId} missing from lookup for {Source}/{Pollutant}/{Window}; dropping slice.",
                                entry.UnitId, tuple.SourceId, tuple.PollutantId, windowStart);
                            continue;
                        }
                        if (UnitConverter.TryToCanonical(
                                entry.Avg.Value, fromUnit, canonicalUnit, canonical.MolarMass,
                                out var canonicalSliceAvg, out var conversionError))
                        {
                            weightedSum += canonicalSliceAvg * entry.SampleCount;
                            totalSampleCount += entry.SampleCount;
                        }
                        else
                        {
                            logger.LogWarning(
                                "Cannot convert {Symbol} to canonical {CanonicalSymbol} for pollutant {Pollutant}: {Reason}; slice dropped.",
                                fromUnit.Symbol, canonicalUnit.Symbol, tuple.PollutantId, conversionError);
                        }
                    }

                    if (totalSampleCount == 0) continue;
                    var canonicalAvg = Math.Round(weightedSum / totalSampleCount, 6);

                    var expected = (int)period.TotalMinutes;
                    var validCount = (int)Math.Min(int.MaxValue, validMinutes);
                    var normalizedValue = TryComputeNormalized(
                        canonicalAvg, o2Ref, tuple.SourceId, windowStart, paramReadings);
                    var existingKey = (tuple.SourceId, tuple.PollutantId, windowStart);

                    if (existingByKey.TryGetValue(existingKey, out var existingMeasurement))
                    {
                        if (existingMeasurement.Value == canonicalAvg
                            && existingMeasurement.NormalizedValue == normalizedValue
                            && existingMeasurement.ValidPointsCount == validCount
                            && existingMeasurement.ExpectedPointsCount == expected
                            && existingMeasurement.UnitId == canonical.CanonicalUnitId)
                        {
                            continue;
                        }

                        existingMeasurement.RecomputeAggregate(
                            canonicalAvg, normalizedValue, validCount, expected, canonical.CanonicalUnitId);
                        await ApplyIedSubstitutionIfNeededAsync(
                            existingMeasurement, tuple, validCount, expected, windowStart, cancellationToken);
                        updatedCount++;
                    }
                    else
                    {
                        if (insertedThisTick >= _settings.BackfillWindowsPerTick) break;
                        // DeviceId is null for materialized aggregates — a window can be built
                        // from multiple devices' raw rows and naming one would be a lie. Raw
                        // device lineage stays queryable via raw_measurement.
                        var measurement = Measurement.New(
                            Guid.NewGuid(),
                            windowStart, windowEnd,
                            tuple.Period, Aggregation.Average,
                            tuple.SourceId, tuple.PollutantId,
                            deviceId: null,
                            unitId: canonical.CanonicalUnitId,
                            value: canonicalAvg,
                            validPointsCount: validCount,
                            expectedPointsCount: expected,
                            normalizedValue: normalizedValue);

                        await ApplyIedSubstitutionIfNeededAsync(
                            measurement, tuple, validCount, expected, windowStart, cancellationToken);

                        newMeasurements.Add(measurement);
                        insertedThisTick++;
                    }
                }
            }
        }

        if (newMeasurements.Count > 0)
        {
            await measurementRepository.AddRangeAsync(newMeasurements, cancellationToken);
        }
        if (newMeasurements.Count > 0 || updatedCount > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Materialized {New} new, updated {Updated} Measurement records in {Ms}ms (enterprise: {Enterprise})",
            newMeasurements.Count, updatedCount, (DateTime.UtcNow - start).TotalMilliseconds,
            enterpriseId?.ToString() ?? "all");
    }

    /// <summary>
    /// IED Annex V Part 4 substitution: when DataAvailability drops below the configured
    /// threshold, the window is non-representative and its computed average cannot be used
    /// for compliance. The customary CEMS substitute is MAX of recent valid windows × 1.05.
    /// If no history is available, the value is left as-is but quality is marked Substituted
    /// so downstream consumers know it is unreliable.
    /// </summary>
    private async Task ApplyIedSubstitutionIfNeededAsync(
        Measurement measurement, MaterializationTuple tuple,
        int validCount, int expectedCount, DateTime windowStart,
        CancellationToken ct)
    {
        if (expectedCount <= 0) return;
        var availability = (decimal)validCount / expectedCount;
        if (availability >= _settings.DataAvailabilityThreshold) return;

        var maxRecent = await queries.GetMaxValueOverRecentValidWindowsAsync(
            tuple.SourceId, tuple.PollutantId, tuple.Period,
            windowStart, _settings.SubstitutionLookbackWindows, ct);

        if (maxRecent is null)
        {
            measurement.MarkSubstituted(SubstitutionSource.Auto,
                $"Availability {availability:P0} < {_settings.DataAvailabilityThreshold:P0}; " +
                "no valid historical windows to derive a substitute.");
            return;
        }

        var substitute = Math.Round(maxRecent.Value * _settings.SubstitutionMultiplier, 6);
        measurement.MarkSubstituted(SubstitutionSource.Auto,
            $"IED substitution: availability {availability:P0} < {_settings.DataAvailabilityThreshold:P0}; " +
            $"substitute = max({maxRecent.Value:0.###}) × {_settings.SubstitutionMultiplier} = {substitute:0.###}",
            substitute);
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
