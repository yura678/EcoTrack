using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Domain.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence.Repositories.Monitoring;

/// <summary>
/// All analytics queries that back ComplianceDetectionService and MeasurementMaterializationService.
/// Centralises raw-SQL access to Timescale continuous aggregates plus a few EF lookups, so the
/// orchestrating services stay free of DbContext + ORM details and can focus on policy.
/// </summary>
internal class ComplianceDetectionQueries(ApplicationDbContext context) : IComplianceDetectionQueries
{
    // ─── Limit & source tuples ───────────────────────────────────────────────────

    public async Task<List<LimitTarget>> GetActiveLimitTargetsAsync(
        IReadOnlyCollection<LimitType> limitTypes, CancellationToken ct,
        Guid? enterpriseId = null)
    {
        var now = DateTime.UtcNow;
        var limits = await context.Set<EmissionLimit>()
            .Where(l => limitTypes.Contains(l.LimitType)
                        && l.ValidFrom <= now
                        && (l.ValidTo == null || l.ValidTo >= now)
                        && l.Permit!.PermitStatus == PermitStatus.Active
                        && l.Permit!.ValidUntil >= now
                        && (enterpriseId == null || l.EnterpriseId == enterpriseId.Value))
            .Select(l => new
            {
                l.Id, l.EmissionSourceId, l.InstallationId, l.PollutantId,
                l.Period, l.Value, l.UnitId, l.LimitType
            })
            .ToListAsync(ct);

        if (limits.Count == 0) return [];

        var sourcesByInstallation = await GetSourcesByInstallationAsync(
            limits.Where(l => l.EmissionSourceId == null && l.InstallationId != null)
                .Select(l => l.InstallationId!.Value)
                .Distinct()
                .ToArray(),
            ct);

        var result = new List<LimitTarget>();
        foreach (var l in limits)
        {
            if (l.EmissionSourceId.HasValue)
            {
                result.Add(new LimitTarget(l.Id, l.EmissionSourceId.Value, l.PollutantId,
                    l.Period, l.Value, l.UnitId, l.LimitType, InstallationId: null));
            }
            else if (l.InstallationId.HasValue
                     && sourcesByInstallation.TryGetValue(l.InstallationId.Value, out var sids))
            {
                foreach (var sid in sids)
                    result.Add(new LimitTarget(l.Id, sid, l.PollutantId, l.Period, l.Value, l.UnitId,
                        l.LimitType, InstallationId: l.InstallationId));
            }
        }
        return result;
    }

    public async Task<Dictionary<Guid, LimitTarget>> GetActiveLimitsByIdsAsync(
        IReadOnlyCollection<Guid> limitIds, CancellationToken ct)
    {
        if (limitIds.Count == 0) return [];
        var now = DateTime.UtcNow;
        var rows = await context.Set<EmissionLimit>()
            .Where(l => limitIds.Contains(l.Id)
                        && l.ValidFrom <= now
                        && (l.ValidTo == null || l.ValidTo >= now)
                        && l.Permit!.PermitStatus == PermitStatus.Active
                        && l.Permit!.ValidUntil >= now)
            .Select(l => new
            {
                l.Id, l.EmissionSourceId, l.InstallationId, l.PollutantId,
                l.Period, l.Value, l.UnitId, l.LimitType
            })
            .ToListAsync(ct);

        var dict = new Dictionary<Guid, LimitTarget>(rows.Count);
        foreach (var l in rows)
        {
            // EmissionSourceId is optional on installation-wide limits; the probe needs a source
            // anchor (the ComplianceEvent already pins the source), so leave empty here and let
            // the caller substitute event.EmissionSourceId.
            dict[l.Id] = new LimitTarget(l.Id, l.EmissionSourceId ?? Guid.Empty,
                l.PollutantId, l.Period, l.Value, l.UnitId, l.LimitType, l.InstallationId);
        }
        return dict;
    }

    public async Task<List<MaterializationTuple>> GetActiveMaterializationTuplesAsync(
        IReadOnlyCollection<LimitType> limitTypes, CancellationToken ct,
        Guid? enterpriseId = null)
    {
        var now = DateTime.UtcNow;
        var limits = await context.Set<EmissionLimit>()
            .Where(l => limitTypes.Contains(l.LimitType)
                        && l.ValidFrom <= now
                        && (l.ValidTo == null || l.ValidTo >= now)
                        && l.Permit!.PermitStatus == PermitStatus.Active
                        && l.Permit!.ValidUntil >= now
                        && (enterpriseId == null || l.EnterpriseId == enterpriseId.Value))
            .Select(l => new { l.EmissionSourceId, l.InstallationId, l.PollutantId, l.Period, l.ValidFrom })
            .ToListAsync(ct);

        var sourcesByInstallation = await GetSourcesByInstallationAsync(
            limits.Where(l => l.EmissionSourceId == null && l.InstallationId != null)
                .Select(l => l.InstallationId!.Value)
                .Distinct()
                .ToArray(),
            ct);

        // Multiple limits can target the same (source, pollutant, period); take the earliest
        // ValidFrom so backfill reaches the oldest active obligation.
        var earliestByKey = new Dictionary<(Guid SourceId, Guid PollutantId, AveragingWindow Period), DateTime>();

        void Record(Guid sourceId, Guid pollutantId, AveragingWindow period, DateTime validFrom)
        {
            var key = (sourceId, pollutantId, period);
            if (!earliestByKey.TryGetValue(key, out var existing) || validFrom < existing)
                earliestByKey[key] = validFrom;
        }

        foreach (var l in limits)
        {
            if (l.EmissionSourceId.HasValue)
            {
                Record(l.EmissionSourceId.Value, l.PollutantId, l.Period, l.ValidFrom);
            }
            else if (l.InstallationId.HasValue
                     && sourcesByInstallation.TryGetValue(l.InstallationId.Value, out var sids))
            {
                foreach (var sid in sids)
                    Record(sid, l.PollutantId, l.Period, l.ValidFrom);
            }
        }

        return earliestByKey
            .Select(kvp => new MaterializationTuple(
                kvp.Key.SourceId, kvp.Key.PollutantId, kvp.Key.Period, kvp.Value))
            .ToList();
    }

    private async Task<Dictionary<Guid, List<Guid>>> GetSourcesByInstallationAsync(
        Guid[] installationIds, CancellationToken ct)
    {
        if (installationIds.Length == 0) return new Dictionary<Guid, List<Guid>>();
        var rows = await context.Set<EmissionSource>()
            .Where(s => installationIds.Contains(s.InstallationId))
            .Select(s => new { s.Id, s.InstallationId })
            .ToListAsync(ct);
        return rows.GroupBy(s => s.InstallationId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
    }

    // ─── Reference lookups ──────────────────────────────────────────────────────

    public async Task<Dictionary<Guid, UnitInfo>> GetUnitsAsync(
        IReadOnlyCollection<Guid> unitIds, CancellationToken ct)
    {
        if (unitIds.Count == 0) return [];
        return await context.Set<MeasureUnit>()
            .Where(u => unitIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Symbol, u.Dimension, u.ToBaseFactor })
            .ToDictionaryAsync(u => u.Id,
                u => new UnitInfo(u.Symbol, u.Dimension, u.ToBaseFactor), ct);
    }

    public async Task<Dictionary<Guid, decimal?>> GetPollutantO2ReferencesAsync(
        IReadOnlyCollection<Guid> pollutantIds, CancellationToken ct)
    {
        if (pollutantIds.Count == 0) return [];
        return await context.Set<Pollutant>()
            .Where(p => pollutantIds.Contains(p.Id))
            .Select(p => new { p.Id, p.DefaultO2Reference })
            .ToDictionaryAsync(p => p.Id, p => p.DefaultO2Reference, ct);
    }

    // ─── Measurement reads (detection) ──────────────────────────────────────────

    public async Task<IReadOnlyList<MeasurementSnapshot>> GetMeasurementsForWindowAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        AveragingWindow period,
        DateTime windowEnd,
        CancellationToken ct)
    {
        if (sourceIds.Count == 0 || pollutantIds.Count == 0) return [];
        var rows = await context.Set<Measurement>()
            .Where(m => sourceIds.Contains(m.EmissionSourceId)
                        && pollutantIds.Contains(m.PollutantId)
                        && m.Window == period
                        && m.Aggregation == Aggregation.Average
                        && m.WindowEnd == windowEnd)
            .Select(m => new MeasurementSnapshot(
                m.Id, m.EmissionSourceId, m.PollutantId,
                m.Value, m.NormalizedValue, m.UnitId, m.Quality,
                m.ValidPointsCount, m.ExpectedPointsCount,
                m.WindowStart, m.WindowEnd, m.Window))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<IReadOnlyList<MeasurementSnapshot>> GetMeasurementsForWindowRangeAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        AveragingWindow period,
        DateTime fromWindowEndInclusive,
        DateTime toWindowEndInclusive,
        CancellationToken ct)
    {
        if (sourceIds.Count == 0 || pollutantIds.Count == 0) return [];
        return await context.Set<Measurement>()
            .Where(m => sourceIds.Contains(m.EmissionSourceId)
                        && pollutantIds.Contains(m.PollutantId)
                        && m.Window == period
                        && m.Aggregation == Aggregation.Average
                        && m.WindowEnd >= fromWindowEndInclusive
                        && m.WindowEnd <= toWindowEndInclusive)
            .Select(m => new MeasurementSnapshot(
                m.Id, m.EmissionSourceId, m.PollutantId,
                m.Value, m.NormalizedValue, m.UnitId, m.Quality,
                m.ValidPointsCount, m.ExpectedPointsCount,
                m.WindowStart, m.WindowEnd, m.Window))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MeasurementSnapshot>> GetLatestMeasurementsAsync(
        IReadOnlyCollection<(Guid SourceId, Guid PollutantId)> pairs,
        AveragingWindow period,
        CancellationToken ct)
    {
        if (pairs.Count == 0) return [];
        var sourceIds = pairs.Select(p => p.SourceId).Distinct().ToArray();
        var pollutantIds = pairs.Select(p => p.PollutantId).Distinct().ToArray();

        // Two-step EF query: first find max(WindowEnd) per (source, pollutant) — translates to
        // a clean GROUP BY — then load the rows whose WindowEnd matches. Avoids the
        // GroupBy(...).Select(g => g.First()) shape that PG translation does not support.
        var maxEnds = await context.Set<Measurement>()
            .Where(m => sourceIds.Contains(m.EmissionSourceId)
                        && pollutantIds.Contains(m.PollutantId)
                        && m.Window == period
                        && m.Aggregation == Aggregation.Average)
            .GroupBy(m => new { m.EmissionSourceId, m.PollutantId })
            .Select(g => new
            {
                g.Key.EmissionSourceId,
                g.Key.PollutantId,
                MaxEnd = g.Max(m => m.WindowEnd)
            })
            .ToListAsync(ct);
        if (maxEnds.Count == 0) return [];

        var maxEndsArr = maxEnds.Select(x => x.MaxEnd).Distinct().ToArray();
        var rows = await context.Set<Measurement>()
            .Where(m => sourceIds.Contains(m.EmissionSourceId)
                        && pollutantIds.Contains(m.PollutantId)
                        && m.Window == period
                        && m.Aggregation == Aggregation.Average
                        && maxEndsArr.Contains(m.WindowEnd))
            .Select(m => new MeasurementSnapshot(
                m.Id, m.EmissionSourceId, m.PollutantId,
                m.Value, m.NormalizedValue, m.UnitId, m.Quality,
                m.ValidPointsCount, m.ExpectedPointsCount,
                m.WindowStart, m.WindowEnd, m.Window))
            .ToListAsync(ct);

        // Keep only the (source, pollutant) row whose WindowEnd actually equals that pair's max,
        // and restrict to originally requested pairs (the IN filter above can over-fetch when
        // different pairs happen to share the same max timestamp).
        var maxByPair = maxEnds.ToDictionary(
            x => (x.EmissionSourceId, x.PollutantId), x => x.MaxEnd);
        var requestedPairs = pairs.ToHashSet();
        return rows
            .Where(r => requestedPairs.Contains((r.EmissionSourceId, r.PollutantId))
                        && maxByPair.TryGetValue(
                            (r.EmissionSourceId, r.PollutantId), out var max)
                        && r.WindowEnd == max)
            .ToList();
    }

    public async Task<IReadOnlyList<MeasurementSnapshot>> GetMeasurementsByIdsAsync(
        IReadOnlyCollection<Guid> measurementIds, CancellationToken ct)
    {
        if (measurementIds.Count == 0) return [];
        return await context.Set<Measurement>()
            .Where(m => measurementIds.Contains(m.Id))
            .Select(m => new MeasurementSnapshot(
                m.Id, m.EmissionSourceId, m.PollutantId,
                m.Value, m.NormalizedValue, m.UnitId, m.Quality,
                m.ValidPointsCount, m.ExpectedPointsCount,
                m.WindowStart, m.WindowEnd, m.Window))
            .ToListAsync(ct);
    }

    public async Task<decimal?> GetMaxValueOverRecentValidWindowsAsync(
        Guid sourceId,
        Guid pollutantId,
        AveragingWindow period,
        DateTime beforeWindowStart,
        int lookbackCount,
        CancellationToken ct)
    {
        if (lookbackCount <= 0) return null;
        var recent = await context.Set<Measurement>()
            .Where(m => m.EmissionSourceId == sourceId
                        && m.PollutantId == pollutantId
                        && m.Window == period
                        && m.Aggregation == Aggregation.Average
                        && m.Quality == Quality.Valid
                        && m.WindowStart < beforeWindowStart)
            .OrderByDescending(m => m.WindowEnd)
            .Take(lookbackCount)
            .Select(m => m.Value)
            .ToListAsync(ct);
        return recent.Count == 0 ? null : recent.Max();
    }

    // ─── Materialization ────────────────────────────────────────────────────────

    public async Task<Dictionary<(Guid SourceId, Guid PollutantId), DateTime?>> GetLastWindowEndsAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        AveragingWindow period,
        CancellationToken ct)
    {
        var rows = await context.Set<Measurement>()
            .Where(m => sourceIds.Contains(m.EmissionSourceId)
                        && pollutantIds.Contains(m.PollutantId)
                        && m.Window == period
                        && m.Aggregation == Aggregation.Average)
            .GroupBy(m => new { m.EmissionSourceId, m.PollutantId })
            .Select(g => new
            {
                g.Key.EmissionSourceId,
                g.Key.PollutantId,
                LastEnd = g.Max(x => x.WindowEnd)
            })
            .ToListAsync(ct);
        return rows.ToDictionary(
            r => (r.EmissionSourceId, r.PollutantId),
            r => (DateTime?)r.LastEnd);
    }

    public async Task<Dictionary<(Guid, Guid), List<AggregateBucket>>> GetReBucketedBulkAsync(
        IReadOnlyCollection<Guid> sourceIds,
        IReadOnlyCollection<Guid> pollutantIds,
        TimeSpan period,
        DateTime from,
        DateTime to,
        CancellationToken ct)
    {
        var periodLiteral = PeriodToPgInterval(period);

        // CTE computes "distinct minutes with any valid data in the window" once per
        // (source, pollutant, window). The main SELECT preserves the per-unit split so the
        // materializer can convert each unit's slice into the pollutant's canonical unit before
        // weighted-averaging. Summing valid_count across per-unit rows would double-count any
        // minute that received data in more than one unit; the CTE avoids that by counting
        // distinct 1-minute buckets.
        var sql = $@"
            WITH valid_minutes AS (
                SELECT
                    emission_source_id,
                    pollutant_id,
                    time_bucket(INTERVAL '{periodLiteral}', bucket) AS window_start,
                    COUNT(DISTINCT bucket) AS minutes_with_valid
                FROM measurement_1m
                WHERE emission_source_id = ANY(@source_ids)
                  AND pollutant_id = ANY(@pollutant_ids)
                  AND bucket >= @from
                  AND bucket < @to
                  AND valid_count > 0
                GROUP BY emission_source_id, pollutant_id, window_start
            ),
            bucketed AS (
                SELECT
                    emission_source_id,
                    pollutant_id,
                    unit_id,
                    time_bucket(INTERVAL '{periodLiteral}', bucket) AS window_start,
                    SUM(sum_value) AS sum_value,
                    SUM(sample_count) AS sample_count
                FROM measurement_1m
                WHERE emission_source_id = ANY(@source_ids)
                  AND pollutant_id = ANY(@pollutant_ids)
                  AND bucket >= @from
                  AND bucket < @to
                GROUP BY emission_source_id, pollutant_id, unit_id, window_start
            )
            SELECT
                b.emission_source_id,
                b.pollutant_id,
                b.window_start,
                b.unit_id,
                (b.sum_value / NULLIF(b.sample_count, 0))::numeric(18,6) AS avg,
                COALESCE(b.sample_count, 0)::bigint AS sample_count,
                COALESCE(v.minutes_with_valid, 0)::bigint AS valid_minutes_in_window
            FROM bucketed b
            LEFT JOIN valid_minutes v ON
                v.emission_source_id = b.emission_source_id
                AND v.pollutant_id = b.pollutant_id
                AND v.window_start = b.window_start
            ORDER BY b.emission_source_id, b.pollutant_id, b.window_start, b.unit_id";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, sourceIds.ToArray());
        AddParam(command, "pollutant_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, pollutantIds.ToArray());
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));

        var dict = new Dictionary<(Guid, Guid), List<AggregateBucket>>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var sourceId = reader.GetGuid(0);
            var pollutantId = reader.GetGuid(1);
            var windowStart = DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc);
            var unitId = reader.GetGuid(3);
            var avg = reader.IsDBNull(4) ? (decimal?)null : reader.GetDecimal(4);
            var sampleCount = reader.GetInt64(5);
            var validMinutes = reader.GetInt64(6);

            var key = (sourceId, pollutantId);
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<AggregateBucket>();
                dict[key] = list;
            }
            list.Add(new AggregateBucket(windowStart, unitId, avg, sampleCount, validMinutes));
        }
        return dict;
    }

    public async Task<Dictionary<Guid, PollutantCanonical>> GetPollutantCanonicalsAsync(
        IReadOnlyCollection<Guid> pollutantIds, CancellationToken ct)
    {
        if (pollutantIds.Count == 0) return [];
        var rows = await context.Set<Pollutant>()
            .Where(p => pollutantIds.Contains(p.Id))
            .Select(p => new { p.Id, p.CanonicalUnitId, p.MolarMass })
            .ToListAsync(ct);
        return rows.ToDictionary(
            r => r.Id,
            r => new PollutantCanonical(r.CanonicalUnitId, r.MolarMass));
    }

    // ─── Devices & calibration ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<OperationalDevice>> GetOperationalDevicesAsync(
        CancellationToken ct, Guid? enterpriseId = null)
    {
        var rows = await context.Set<MonitoringDevice>()
            .Where(d => d.Status == DeviceStatus.Operational && d.EmissionSourceId != null
                        && (enterpriseId == null || d.EnterpriseId == enterpriseId.Value))
            .Select(d => new OperationalDevice(d.Id, d.EmissionSourceId!.Value, d.InstalledAt))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<IReadOnlyList<DeviceCalibrationSnapshot>> GetDevicesWithLatestCalibrationAsync(
        CancellationToken ct, Guid? enterpriseId = null)
    {
        // Subquery in Select is translated to LATERAL JOIN by Npgsql provider — one SQL trip.
        var rows = await context.Set<MonitoringDevice>()
            .Where(d => d.Status == DeviceStatus.Operational && d.EmissionSourceId != null
                        && (enterpriseId == null || d.EnterpriseId == enterpriseId.Value))
            .Select(d => new
            {
                d.Id,
                EmissionSourceId = d.EmissionSourceId!.Value,
                d.InstalledAt,
                Last = context.Set<CalibrationRecord>()
                    .Where(c => c.DeviceId == d.Id)
                    .OrderByDescending(c => c.PerformedAt)
                    .Select(c => new { c.Result, c.PerformedAt, c.NextDueAt })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Select(r => new DeviceCalibrationSnapshot(
            r.Id, r.EmissionSourceId, r.InstalledAt,
            r.Last == null ? null : r.Last.Result,
            r.Last?.PerformedAt,
            r.Last?.NextDueAt)).ToList();
    }

    public async Task<Dictionary<Guid, DateTime?>> GetDeviceLastSeenAsync(
        IReadOnlyCollection<Guid> deviceIds, DateTime since, CancellationToken ct)
    {
        if (deviceIds.Count == 0) return [];

        // The time >= @since predicate is what makes this query Timescale-friendly: it lets the
        // planner prune all chunks older than the window and reduces the scan to the one or two
        // newest chunks. Without it the planner has to MAX-aggregate over every chunk ever
        // written, which times out once the hypertable has a few months of data.
        // Callers are expected to pass their offline cutoff — anything older is irrelevant to
        // the "is this device online right now" decision they're making.
        var sql = @"
            SELECT device_id, MAX(time) AS last_seen
            FROM raw_measurement
            WHERE device_id = ANY(@device_ids)
              AND time >= @since
            GROUP BY device_id";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "device_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, deviceIds.ToArray());
        AddParam(command, "since", NpgsqlDbType.TimestampTz, EnsureUtc(since));

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

    // ─── Process parameters ─────────────────────────────────────────────────────

    public async Task<Dictionary<(Guid, DateTime), ProcessParamReadings>>
        GetProcessParameterAveragesAsync(
            IReadOnlyCollection<Guid> sourceIds,
            TimeSpan period,
            DateTime from,
            DateTime to,
            CancellationToken ct)
    {
        var periodLiteral = PeriodToPgInterval(period);
        // ParameterType: StackTemperature=0, StackPressure=1, O2Content=2, MoistureContent=3.
        var sql = $@"
            SELECT
                emission_source_id,
                parameter_type,
                time_bucket(INTERVAL '{periodLiteral}', bucket) AS window_start,
                (SUM(valid_sum) / NULLIF(SUM(valid_count), 0))::numeric(18,6) AS avg_value
            FROM process_parameter_1m
            WHERE emission_source_id = ANY(@source_ids)
              AND parameter_type = ANY(ARRAY[0, 1, 2, 3])
              AND bucket >= @from
              AND bucket < @to
            GROUP BY emission_source_id, parameter_type, window_start
            HAVING SUM(valid_count) > 0";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, sourceIds.ToArray());
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));

        var byKey = new Dictionary<(Guid, DateTime), Dictionary<int, decimal>>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var sourceId = reader.GetGuid(0);
            var paramType = reader.GetInt32(1);
            var windowStart = DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc);
            var avg = reader.GetDecimal(3);

            var key = (sourceId, windowStart);
            if (!byKey.TryGetValue(key, out var perType))
            {
                perType = new Dictionary<int, decimal>();
                byKey[key] = perType;
            }
            perType[paramType] = avg;
        }

        var result = new Dictionary<(Guid, DateTime), ProcessParamReadings>(byKey.Count);
        foreach (var (key, perType) in byKey)
        {
            result[key] = new ProcessParamReadings(
                O2Percent: perType.TryGetValue(2, out var o2) ? o2 : null,
                TemperatureCelsius: perType.TryGetValue(0, out var t) ? t : null,
                PressureKPa: perType.TryGetValue(1, out var p) ? p : null,
                MoisturePercent: perType.TryGetValue(3, out var h) ? h : null);
        }
        return result;
    }

    public async Task<Dictionary<Guid, decimal>> GetO2AverageForRangeAsync(
        IReadOnlyCollection<Guid> sourceIds, DateTime from, DateTime to, CancellationToken ct)
    {
        if (sourceIds.Count == 0) return [];
        var sql = @"
            SELECT
                emission_source_id,
                (SUM(valid_sum) / NULLIF(SUM(valid_count), 0))::numeric(18,6) AS avg_o2
            FROM process_parameter_1m
            WHERE emission_source_id = ANY(@source_ids)
              AND parameter_type = 2  -- ParameterType.O2Content
              AND bucket >= @from
              AND bucket < @to
            GROUP BY emission_source_id
            HAVING SUM(valid_count) > 0";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, sourceIds.ToArray());
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));

        var dict = new Dictionary<Guid, decimal>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            dict[reader.GetGuid(0)] = reader.GetDecimal(1);
        }
        return dict;
    }

    public async Task<Dictionary<Guid, FlowReading>> GetVolumetricFlowForRangeAsync(
        IReadOnlyCollection<Guid> sourceIds, DateTime from, DateTime to, CancellationToken ct)
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
        AddParam(command, "source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, sourceIds.ToArray());
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));

        var dict = new Dictionary<Guid, FlowReading>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var sourceId = reader.GetGuid(0);
            var value = reader.GetDecimal(1);
            var unitId = reader.GetGuid(2);
            dict[sourceId] = new FlowReading(value, unitId);
        }
        return dict;
    }

    // ─── Raw counts & long-window rolling stats ─────────────────────────────────

    public async Task<Dictionary<Guid, long>> GetRawMeasurementCountsBySourceAsync(
        IReadOnlyCollection<Guid> sourceIds, DateTime from, DateTime to, CancellationToken ct)
    {
        if (sourceIds.Count == 0) return [];
        var sql = @"
            SELECT emission_source_id, COUNT(*)::bigint
            FROM raw_measurement
            WHERE emission_source_id = ANY(@source_ids)
              AND time >= @from AND time < @to
            GROUP BY emission_source_id";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, sourceIds.ToArray());
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));

        var dict = new Dictionary<Guid, long>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            dict[reader.GetGuid(0)] = reader.GetInt64(1);
        }
        return dict;
    }

    public async Task<Dictionary<(Guid, Guid), long>> GetRawMeasurementCountsAsync(
        IReadOnlyCollection<Guid> sourceIds, IReadOnlyCollection<Guid> pollutantIds,
        DateTime from, DateTime to, CancellationToken ct)
    {
        var sql = @"
            SELECT emission_source_id, pollutant_id, COUNT(*)::bigint
            FROM raw_measurement
            WHERE emission_source_id = ANY(@source_ids)
              AND pollutant_id = ANY(@pollutant_ids)
              AND time >= @from AND time < @to
            GROUP BY emission_source_id, pollutant_id";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, sourceIds.ToArray());
        AddParam(command, "pollutant_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, pollutantIds.ToArray());
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

    public async Task<Dictionary<(Guid SourceId, Guid PollutantId), RollingAverage>>
        GetRollingAverageRateAsync(
            IReadOnlyCollection<Guid> sourceIds, IReadOnlyCollection<Guid> pollutantIds,
            DateTime from, DateTime to, CancellationToken ct)
    {
        // Per-(source, pollutant, unit_id) valid-only slice. C# folds slices into the pollutant's
        // canonical unit so AnnualLoad detection compares apples to apples across device-swaps
        // that changed the reporting unit mid-period (Phase 5c).
        var sql = @"
            SELECT
                m.emission_source_id,
                m.pollutant_id,
                m.unit_id,
                SUM(m.valid_sum)::numeric(18,6) AS valid_sum,
                COALESCE(SUM(m.valid_count), 0)::bigint AS valid_count
            FROM measurement_1m m
            JOIN emission_source es ON es.id = m.emission_source_id
            WHERE m.emission_source_id = ANY(@source_ids)
              AND m.pollutant_id = ANY(@pollutant_ids)
              AND m.bucket >= @from
              AND m.bucket < @to
              AND es.deleted_at IS NULL
            GROUP BY m.emission_source_id, m.pollutant_id, m.unit_id
            HAVING SUM(m.valid_count) > 0";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, sourceIds.ToArray());
        AddParam(command, "pollutant_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, pollutantIds.ToArray());
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));

        var slices = new List<RollingSlice>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                slices.Add(new RollingSlice(
                    SourceId: reader.GetGuid(0),
                    PollutantId: reader.GetGuid(1),
                    UnitId: reader.GetGuid(2),
                    ValidSum: reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                    ValidCount: reader.GetInt64(4)));
            }
        }
        if (slices.Count == 0) return [];

        var distinctPollutantIds = slices.Select(s => s.PollutantId).Distinct().ToArray();
        var canonicals = await GetPollutantCanonicalsAsync(distinctPollutantIds, ct);
        var distinctUnitIds = slices.Select(s => s.UnitId)
            .Concat(canonicals.Values.Select(c => c.CanonicalUnitId))
            .Distinct()
            .ToArray();
        var unitInfos = await GetUnitsAsync(distinctUnitIds, ct);
        // Rehydrate UnitInfo into MeasureUnit shadows so UnitConverter (which keys identity off
        // MeasureUnit.Id) can short-circuit when from-unit equals canonical.
        var unitEntities = unitInfos.ToDictionary(
            kvp => kvp.Key,
            kvp => MeasureUnit.New(kvp.Key, kvp.Value.Symbol, kvp.Value.Dimension, kvp.Value.ToBaseFactor));

        var dict = new Dictionary<(Guid, Guid), RollingAverage>();
        foreach (var byKey in slices.GroupBy(s => (s.SourceId, s.PollutantId)))
        {
            if (!canonicals.TryGetValue(byKey.Key.PollutantId, out var canonical)) continue;
            if (!unitEntities.TryGetValue(canonical.CanonicalUnitId, out var canonicalUnit)) continue;

            decimal canonicalValidSumTotal = 0m;
            long validCountTotal = 0;
            var anyConverted = false;
            foreach (var s in byKey)
            {
                if (!unitEntities.TryGetValue(s.UnitId, out var fromUnit)) continue;
                if (!UnitConverter.TryToCanonical(
                        s.ValidSum, fromUnit, canonicalUnit, canonical.MolarMass,
                        out var canonicalValidSum, out _)) continue;
                canonicalValidSumTotal += canonicalValidSum;
                validCountTotal += s.ValidCount;
                anyConverted = true;
            }
            if (!anyConverted || validCountTotal == 0) continue;

            var avgRate = Math.Round(canonicalValidSumTotal / validCountTotal, 6);
            dict[byKey.Key] = new RollingAverage(avgRate, validCountTotal, canonical.CanonicalUnitId);
        }
        return dict;
    }

    private record RollingSlice(Guid SourceId, Guid PollutantId, Guid UnitId, decimal ValidSum, long ValidCount);

    public async Task<IReadOnlyList<OutOfRangeWindow>> GetOutOfRangeWindowsAsync(
        DateTime from, DateTime to, decimal threshold, int minSampleCount, CancellationToken ct,
        Guid? enterpriseId = null)
    {
        // Quality.Invalid is 3 — see Domain.Entities.Monitoring.Quality enum.
        // Per-tenant scope: JOIN to emission_source only when enterpriseId is provided so the
        // unscoped legacy hot path keeps its hypertable-friendly single-table scan.
        var tenantJoin = enterpriseId.HasValue
            ? "JOIN emission_source es ON es.id = rm.emission_source_id AND es.enterprise_id = @enterprise_id"
            : "";
        var sql = $@"
            SELECT
                rm.emission_source_id,
                rm.device_id,
                rm.pollutant_id,
                COUNT(*)::bigint AS total,
                COUNT(*) FILTER (WHERE rm.quality = 3)::bigint AS invalid_count
            FROM raw_measurement rm
            {tenantJoin}
            WHERE rm.time >= @from AND rm.time < @to
            GROUP BY rm.emission_source_id, rm.device_id, rm.pollutant_id
            HAVING COUNT(*) >= @min_samples
               AND (COUNT(*) FILTER (WHERE rm.quality = 3))::numeric / COUNT(*) > @threshold";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));
        AddParam(command, "min_samples", NpgsqlDbType.Bigint, (long)minSampleCount);
        AddParam(command, "threshold", NpgsqlDbType.Numeric, threshold);
        if (enterpriseId.HasValue)
        {
            AddParam(command, "enterprise_id", NpgsqlDbType.Uuid, enterpriseId.Value);
        }

        var list = new List<OutOfRangeWindow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var sourceId = reader.GetGuid(0);
            var deviceId = reader.GetGuid(1);
            var pollutantId = reader.GetGuid(2);
            var total = reader.GetInt64(3);
            var invalidCount = reader.GetInt64(4);
            var ratio = Math.Round((decimal)invalidCount / total, 4);
            list.Add(new OutOfRangeWindow(sourceId, deviceId, pollutantId, total, invalidCount, ratio));
        }
        return list;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

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

    private static string PeriodToPgInterval(TimeSpan period)
    {
        if (period == TimeSpan.FromMinutes(1)) return "1 minute";
        if (period == TimeSpan.FromMinutes(10)) return "10 minutes";
        if (period == TimeSpan.FromMinutes(30)) return "30 minutes";
        if (period == TimeSpan.FromHours(1)) return "1 hour";
        if (period == TimeSpan.FromHours(24)) return "1 day";
        throw new ArgumentOutOfRangeException(nameof(period), period, null);
    }
}
