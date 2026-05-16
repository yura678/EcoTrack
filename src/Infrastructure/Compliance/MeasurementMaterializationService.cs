using Application.Common.Interfaces.Persistence;
using Application.Common.Settings;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Compliance;

public class MeasurementMaterializationService(
    ApplicationDbContext context,
    IUnitOfWork unitOfWork,
    IOptions<ComplianceDetectionSettings> options,
    ILogger<MeasurementMaterializationService> logger)
{
    private readonly ComplianceDetectionSettings _settings = options.Value;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var tuples = await GetMaterializationTuplesAsync(cancellationToken);
        if (tuples.Count == 0)
        {
            logger.LogDebug("Materialization skipped: no active limits.");
            return;
        }

        var sourceIds = tuples.Select(t => t.SourceId).Distinct().ToArray();
        var deviceLookup = await GetFirstDevicePerSourceAsync(sourceIds, cancellationToken);

        var newMeasurements = new List<Measurement>();

        foreach (var tuple in tuples)
        {
            if (!deviceLookup.TryGetValue(tuple.SourceId, out var deviceId))
            {
                continue; // No device — cannot construct Measurement (FK requirement)
            }

            var period = PeriodToTimeSpan(tuple.Period);
            if (period == TimeSpan.Zero) continue;

            var lastWindowEnd = await GetLastWindowEndAsync(
                tuple.SourceId, tuple.PollutantId, tuple.Period, cancellationToken);

            var now = DateTime.UtcNow;
            var lastClosedEnd = FloorToPeriod(now, period);
            var earliest = lastWindowEnd
                           ?? lastClosedEnd - TimeSpan.FromDays(Math.Max(1, _settings.BackfillDays));

            var buckets = await GetReBucketedAsync(
                tuple.SourceId, tuple.PollutantId, period, earliest, lastClosedEnd, cancellationToken);
            if (buckets.Count == 0) continue;

            // Iterate forward from earliest to lastClosedEnd, capped per tick.
            var windowsThisTick = 0;
            foreach (var entry in buckets.OrderBy(b => b.Key))
            {
                if (windowsThisTick >= _settings.BackfillWindowsPerTick) break;

                var windowStart = entry.Key;
                var windowEnd = windowStart + period;
                if (windowEnd > lastClosedEnd) break;        // skip not-yet-closed
                if (lastWindowEnd.HasValue && windowEnd <= lastWindowEnd.Value) continue; // already done

                var agg = entry.Value;
                if (agg.SampleCount == 0 || agg.Avg is null) continue;

                var expected = (int)period.TotalMinutes;
                var measurement = Measurement.New(
                    Guid.NewGuid(),
                    windowStart, windowEnd,
                    tuple.Period, Aggregation.Average,
                    tuple.SourceId, tuple.PollutantId, deviceId, agg.UnitId,
                    value: agg.Avg.Value,
                    validPointsCount: (int)Math.Min(int.MaxValue, agg.ValidCount),
                    expectedPointsCount: expected);

                newMeasurements.Add(measurement);
                windowsThisTick++;
            }
        }

        if (newMeasurements.Count > 0)
        {
            await context.Set<Measurement>().AddRangeAsync(newMeasurements, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Materialized {Count} Measurement records in {Ms}ms",
            newMeasurements.Count, (DateTime.UtcNow - start).TotalMilliseconds);
    }

    // ─── Tuples + lookups ────────────────────────────────────────────────────────

    private record Tuple(Guid SourceId, Guid PollutantId, AveragingWindow Period);

    private async Task<List<Tuple>> GetMaterializationTuplesAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var limits = await context.Set<EmissionLimit>()
            .Where(l => l.LimitType == LimitType.Concentration
                        && l.ValidFrom <= now
                        && (l.ValidTo == null || l.ValidTo >= now)
                        && l.Permit!.PermitStatus == PermitStatus.Active
                        && l.Permit!.ValidUntil >= now)
            .Select(l => new
            {
                l.EmissionSourceId, l.InstallationId, l.PollutantId, l.Period
            })
            .ToListAsync(ct);

        var installationIds = limits
            .Where(l => l.EmissionSourceId == null && l.InstallationId != null)
            .Select(l => l.InstallationId!.Value)
            .Distinct()
            .ToArray();

        var sourcesByInstallation = installationIds.Length == 0
            ? new Dictionary<Guid, List<Guid>>()
            : (await context.Set<EmissionSource>()
                    .Where(s => installationIds.Contains(s.InstallationId))
                    .Select(s => new { s.Id, s.InstallationId })
                    .ToListAsync(ct))
                .GroupBy(s => s.InstallationId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var tuples = new HashSet<Tuple>();
        foreach (var l in limits)
        {
            if (PeriodToTimeSpan(l.Period) == TimeSpan.Zero) continue;

            if (l.EmissionSourceId.HasValue)
            {
                tuples.Add(new Tuple(l.EmissionSourceId.Value, l.PollutantId, l.Period));
            }
            else if (l.InstallationId.HasValue
                     && sourcesByInstallation.TryGetValue(l.InstallationId.Value, out var sids))
            {
                foreach (var sid in sids)
                    tuples.Add(new Tuple(sid, l.PollutantId, l.Period));
            }
        }
        return tuples.ToList();
    }

    private async Task<Dictionary<Guid, Guid>> GetFirstDevicePerSourceAsync(
        Guid[] sourceIds, CancellationToken ct)
    {
        var rows = await context.Set<MonitoringDevice>()
            .Where(d => d.EmissionSourceId != null && sourceIds.Contains(d.EmissionSourceId!.Value))
            .Select(d => new { d.Id, SourceId = d.EmissionSourceId!.Value })
            .ToListAsync(ct);

        return rows
            .GroupBy(d => d.SourceId)
            .ToDictionary(g => g.Key, g => g.First().Id);
    }

    private async Task<DateTime?> GetLastWindowEndAsync(
        Guid sourceId, Guid pollutantId, AveragingWindow period, CancellationToken ct)
    {
        return await context.Set<Measurement>()
            .Where(m => m.EmissionSourceId == sourceId
                        && m.PollutantId == pollutantId
                        && m.Window == period
                        && m.Aggregation == Aggregation.Average)
            .OrderByDescending(m => m.WindowEnd)
            .Select(m => (DateTime?)m.WindowEnd)
            .FirstOrDefaultAsync(ct);
    }

    // ─── Re-bucket measurement_1m to the limit's Period ──────────────────────────

    private record AggregateBucket(decimal? Avg, long ValidCount, long SampleCount, Guid UnitId);

    private async Task<Dictionary<DateTime, AggregateBucket>> GetReBucketedAsync(
        Guid sourceId, Guid pollutantId, TimeSpan period,
        DateTime from, DateTime to, CancellationToken ct)
    {
        var periodLiteral = PeriodToPgInterval(period);
        var sql = $@"
            SELECT
                time_bucket(INTERVAL '{periodLiteral}', bucket) AS window_start,
                (SUM(sum_value) / NULLIF(SUM(sample_count), 0))::numeric(18,6) AS avg,
                COALESCE(SUM(valid_count), 0)::bigint AS valid_count,
                COALESCE(SUM(sample_count), 0)::bigint AS sample_count,
                (array_agg(unit_id))[1] AS unit_id
            FROM measurement_1m
            WHERE emission_source_id = @source_id
              AND pollutant_id = @pollutant_id
              AND bucket >= @from
              AND bucket < @to
            GROUP BY window_start
            ORDER BY window_start";

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var command = new NpgsqlCommand(sql, connection);
        AddParam(command, "source_id", NpgsqlDbType.Uuid, sourceId);
        AddParam(command, "pollutant_id", NpgsqlDbType.Uuid, pollutantId);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));

        var dict = new Dictionary<DateTime, AggregateBucket>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var windowStart = DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc);
            var avg = reader.IsDBNull(1) ? (decimal?)null : reader.GetDecimal(1);
            var validCount = reader.GetInt64(2);
            var sampleCount = reader.GetInt64(3);
            var unitId = reader.GetGuid(4);
            dict[windowStart] = new AggregateBucket(avg, validCount, sampleCount, unitId);
        }
        return dict;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

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
