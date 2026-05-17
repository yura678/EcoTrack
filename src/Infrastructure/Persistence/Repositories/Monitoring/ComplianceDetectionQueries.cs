using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
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
        IReadOnlyCollection<LimitType> limitTypes, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var limits = await context.Set<EmissionLimit>()
            .Where(l => limitTypes.Contains(l.LimitType)
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

    public async Task<List<MaterializationTuple>> GetActiveMaterializationTuplesAsync(
        IReadOnlyCollection<LimitType> limitTypes, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var limits = await context.Set<EmissionLimit>()
            .Where(l => limitTypes.Contains(l.LimitType)
                        && l.ValidFrom <= now
                        && (l.ValidTo == null || l.ValidTo >= now)
                        && l.Permit!.PermitStatus == PermitStatus.Active
                        && l.Permit!.ValidUntil >= now)
            .Select(l => new { l.EmissionSourceId, l.InstallationId, l.PollutantId, l.Period })
            .ToListAsync(ct);

        var sourcesByInstallation = await GetSourcesByInstallationAsync(
            limits.Where(l => l.EmissionSourceId == null && l.InstallationId != null)
                .Select(l => l.InstallationId!.Value)
                .Distinct()
                .ToArray(),
            ct);

        var tuples = new HashSet<MaterializationTuple>();
        foreach (var l in limits)
        {
            if (l.EmissionSourceId.HasValue)
            {
                tuples.Add(new MaterializationTuple(l.EmissionSourceId.Value, l.PollutantId, l.Period));
            }
            else if (l.InstallationId.HasValue
                     && sourcesByInstallation.TryGetValue(l.InstallationId.Value, out var sids))
            {
                foreach (var sid in sids)
                    tuples.Add(new MaterializationTuple(sid, l.PollutantId, l.Period));
            }
        }
        return tuples.ToList();
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

    public async Task<Dictionary<Guid, Guid>> GetFirstDevicePerSourceAsync(
        IReadOnlyCollection<Guid> sourceIds, CancellationToken ct)
    {
        if (sourceIds.Count == 0) return [];
        var rows = await context.Set<MonitoringDevice>()
            .Where(d => d.EmissionSourceId != null && sourceIds.Contains(d.EmissionSourceId!.Value))
            .Select(d => new { d.Id, SourceId = d.EmissionSourceId!.Value })
            .ToListAsync(ct);
        return rows.GroupBy(d => d.SourceId).ToDictionary(g => g.Key, g => g.First().Id);
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
                m.WindowStart, m.WindowEnd))
            .ToListAsync(ct);
        return rows;
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
        var sql = $@"
            SELECT
                emission_source_id,
                pollutant_id,
                time_bucket(INTERVAL '{periodLiteral}', bucket) AS window_start,
                (SUM(sum_value) / NULLIF(SUM(sample_count), 0))::numeric(18,6) AS avg,
                COALESCE(SUM(valid_count), 0)::bigint AS valid_count,
                COALESCE(SUM(sample_count), 0)::bigint AS sample_count,
                (array_agg(unit_id))[1] AS unit_id
            FROM measurement_1m
            WHERE emission_source_id = ANY(@source_ids)
              AND pollutant_id = ANY(@pollutant_ids)
              AND bucket >= @from
              AND bucket < @to
            GROUP BY emission_source_id, pollutant_id, window_start
            ORDER BY emission_source_id, pollutant_id, window_start";

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
            var avg = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3);
            var validCount = reader.GetInt64(4);
            var sampleCount = reader.GetInt64(5);
            var unitId = reader.GetGuid(6);

            var key = (sourceId, pollutantId);
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<AggregateBucket>();
                dict[key] = list;
            }
            list.Add(new AggregateBucket(windowStart, avg, validCount, sampleCount, unitId));
        }
        return dict;
    }

    // ─── Devices & calibration ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<OperationalDevice>> GetOperationalDevicesAsync(CancellationToken ct)
    {
        var rows = await context.Set<MonitoringDevice>()
            .Where(d => d.Status == DeviceStatus.Operational && d.EmissionSourceId != null)
            .Select(d => new OperationalDevice(d.Id, d.EmissionSourceId!.Value, d.InstalledAt))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<IReadOnlyList<DeviceCalibrationSnapshot>> GetDevicesWithLatestCalibrationAsync(
        CancellationToken ct)
    {
        // Subquery in Select is translated to LATERAL JOIN by Npgsql provider — one SQL trip.
        var rows = await context.Set<MonitoringDevice>()
            .Where(d => d.Status == DeviceStatus.Operational && d.EmissionSourceId != null)
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
        IReadOnlyCollection<Guid> deviceIds, CancellationToken ct)
    {
        if (deviceIds.Count == 0) return [];
        var sql = @"
            SELECT device_id, MAX(time) AS last_seen
            FROM raw_measurement
            WHERE device_id = ANY(@device_ids)
            GROUP BY device_id";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "device_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, deviceIds.ToArray());

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
        // Reads from measurement_1m (1-min CA) for ~60× faster annual scans vs raw_measurement.
        // valid_sum / valid_count gives a true valid-only average.
        var sql = @"
            SELECT
                m.emission_source_id,
                m.pollutant_id,
                (SUM(m.valid_sum) / NULLIF(SUM(m.valid_count), 0))::numeric(18,6) AS avg_rate,
                COALESCE(SUM(m.valid_count), 0)::bigint AS samples,
                (array_agg(m.unit_id))[1] AS unit_id
            FROM measurement_1m m
            JOIN emission_source es ON es.id = m.emission_source_id
            WHERE m.emission_source_id = ANY(@source_ids)
              AND m.pollutant_id = ANY(@pollutant_ids)
              AND m.bucket >= @from
              AND m.bucket < @to
              AND es.deleted_at IS NULL
            GROUP BY m.emission_source_id, m.pollutant_id
            HAVING SUM(m.valid_count) > 0";

        await using var command = await CreateCommandAsync(sql, ct);
        AddParam(command, "source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, sourceIds.ToArray());
        AddParam(command, "pollutant_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, pollutantIds.ToArray());
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));

        var dict = new Dictionary<(Guid, Guid), RollingAverage>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var sourceId = reader.GetGuid(0);
            var pollutantId = reader.GetGuid(1);
            var avg = reader.GetDecimal(2);
            var samples = reader.GetInt64(3);
            var unitId = reader.GetGuid(4);
            dict[(sourceId, pollutantId)] = new RollingAverage(avg, samples, unitId);
        }
        return dict;
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
