using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.Monitoring;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class RawMeasurementQueries(ApplicationDbContext context) : IRawMeasurementQueries
{
    public Task<IReadOnlyList<TimeSeriesPoint>> GetTimeSeriesAsync(
        Guid pollutantId,
        Guid emissionSourceId,
        DateTime from,
        DateTime to,
        BucketWindow window,
        AggregationFunc aggregation,
        CancellationToken cancellationToken) =>
        aggregation == AggregationFunc.P95
            ? GetTimeSeriesFromRawAsync(pollutantId, emissionSourceId, from, to, window, cancellationToken)
            : GetTimeSeriesFromCaAsync(pollutantId, emissionSourceId, from, to, window, aggregation, cancellationToken);

    public Task<IReadOnlyList<HeatmapPoint>> GetHeatmapAsync(
        Guid pollutantId,
        DateTime from,
        DateTime to,
        AggregationFunc aggregation,
        CancellationToken cancellationToken) =>
        aggregation == AggregationFunc.P95
            ? GetHeatmapFromRawAsync(pollutantId, from, to, cancellationToken)
            : GetHeatmapFromCaAsync(pollutantId, from, to, aggregation, cancellationToken);

    // ─── Compliance audit (what-if read-only) ────────────────────────────────────

    public async Task<ComplianceAuditResult?> GetComplianceAuditAsync(
        ComplianceAuditQueryParams query, CancellationToken cancellationToken)
    {
        var limitUnit = await context.Set<MeasureUnit>()
            .Where(u => u.Id == query.LimitUnitId)
            .Select(u => new { u.Symbol, u.Dimension, u.ToBaseFactor })
            .FirstOrDefaultAsync(cancellationToken);
        if (limitUnit is null) return null;

        var period = PeriodToTimeSpan(query.Period);
        if (period == TimeSpan.Zero) return null;

        var periodLiteral = PeriodToPgInterval(period);
        var limitInBase = query.LimitValue * limitUnit.ToBaseFactor;

        var sql = $@"
            WITH bucketed AS (
                SELECT
                    time_bucket(INTERVAL '{periodLiteral}', m.bucket) AS window_start,
                    (SUM(m.sum_value) / NULLIF(SUM(m.sample_count), 0)) AS avg_raw,
                    (array_agg(m.unit_id))[1] AS unit_id
                FROM measurement_1m m
                WHERE m.emission_source_id = @source_id
                  AND m.pollutant_id = @pollutant_id
                  AND m.bucket >= @from
                  AND m.bucket < @to
                GROUP BY window_start
            ),
            converted AS (
                SELECT b.avg_raw * u.to_base_factor AS value_base
                FROM bucketed b
                JOIN measure_unit u ON u.id = b.unit_id
                WHERE u.dimension = @limit_dimension
                  AND u.deleted_at IS NULL
                  AND b.avg_raw IS NOT NULL
            )
            SELECT
                COUNT(*)::bigint AS buckets_with_data,
                COUNT(*) FILTER (WHERE value_base > @limit_base)::bigint AS exceedance_buckets,
                MAX(value_base)::numeric(18,6) AS max_value,
                AVG(value_base)::numeric(18,6) AS avg_value,
                MAX(value_base / NULLIF(@limit_base, 0))::numeric(18,6) AS max_ratio
            FROM converted";

        await using var command = await CreateCommandAsync(sql, cancellationToken);
        AddParam(command, "source_id", NpgsqlDbType.Uuid, query.EmissionSourceId);
        AddParam(command, "pollutant_id", NpgsqlDbType.Uuid, query.PollutantId);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(query.From));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(query.To));
        AddParam(command, "limit_dimension", NpgsqlDbType.Integer, (int)limitUnit.Dimension);
        AddParam(command, "limit_base", NpgsqlDbType.Numeric, limitInBase);

        long bucketsWithData = 0;
        long exceedanceBuckets = 0;
        decimal? maxValue = null, avgValue = null, maxRatio = null;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            bucketsWithData = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
            exceedanceBuckets = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
            maxValue = reader.IsDBNull(2) ? (decimal?)null : reader.GetDecimal(2);
            avgValue = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3);
            maxRatio = reader.IsDBNull(4) ? (decimal?)null : reader.GetDecimal(4);
        }

        var totalBuckets = (int)Math.Max(0, (query.To - query.From).Ticks / period.Ticks);
        var dataAvailability = totalBuckets == 0
            ? 0m
            : Math.Round((decimal)bucketsWithData / totalBuckets, 4);
        var exceedanceRate = bucketsWithData == 0
            ? (decimal?)null
            : Math.Round((decimal)exceedanceBuckets / bucketsWithData, 4);

        return new ComplianceAuditResult(
            From: EnsureUtc(query.From),
            To: EnsureUtc(query.To),
            Period: query.Period,
            LimitValueInBase: limitInBase,
            LimitUnitSymbol: limitUnit.Symbol,
            TotalBuckets: totalBuckets,
            BucketsWithData: bucketsWithData,
            ExceedanceBuckets: exceedanceBuckets,
            MaxValueInBase: maxValue,
            AvgValueInBase: avgValue,
            MaxRatio: maxRatio,
            ExceedanceRate: exceedanceRate,
            DataAvailability: dataAvailability);
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

    // ─── Continuous-aggregate path (avg/max/min/sum) ────────────────────────────

    private async Task<IReadOnlyList<TimeSeriesPoint>> GetTimeSeriesFromCaAsync(
        Guid pollutantId, Guid emissionSourceId, DateTime from, DateTime to,
        BucketWindow window, AggregationFunc aggregation, CancellationToken cancellationToken)
    {
        var bucketLiteral = BucketLiteral(window);
        var aggExpr = CaAggregationExpression(aggregation);
        var tenantClause = BuildTenantClause();

        var sql = $@"
            SELECT
                time_bucket(INTERVAL '{bucketLiteral}', m.bucket) AS bucket_start,
                ({aggExpr})::numeric(18,6) AS value,
                SUM(m.sample_count)::int AS total_count,
                SUM(m.valid_count)::int AS valid_count
            FROM measurement_1m m
            JOIN emission_source es ON es.id = m.emission_source_id
            WHERE m.pollutant_id = @pollutant_id
              AND m.emission_source_id = @emission_source_id
              AND m.bucket >= @from
              AND m.bucket < @to
              AND es.deleted_at IS NULL
              {tenantClause}
            GROUP BY bucket_start
            ORDER BY bucket_start";

        await using var command = await CreateCommandAsync(sql, cancellationToken);
        AddParam(command, "pollutant_id", NpgsqlDbType.Uuid, pollutantId);
        AddParam(command, "emission_source_id", NpgsqlDbType.Uuid, emissionSourceId);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));
        AddTenantParam(command);

        return await ReadTimeSeriesAsync(command, cancellationToken);
    }

    private async Task<IReadOnlyList<HeatmapPoint>> GetHeatmapFromCaAsync(
        Guid pollutantId, DateTime from, DateTime to,
        AggregationFunc aggregation, CancellationToken cancellationToken)
    {
        var aggExpr = CaAggregationExpression(aggregation);
        var tenantClause = BuildTenantClause();

        var sql = $@"
            SELECT
                es.id AS emission_source_id,
                ST_Y(es.location) AS latitude,
                ST_X(es.location) AS longitude,
                ({aggExpr})::numeric(18,6) AS value,
                (array_agg(m.unit_id))[1] AS unit_id,
                SUM(m.sample_count)::int AS total_count,
                SUM(m.valid_count)::int AS valid_count
            FROM measurement_1m m
            JOIN emission_source es ON es.id = m.emission_source_id
            WHERE m.pollutant_id = @pollutant_id
              AND m.bucket >= @from
              AND m.bucket < @to
              AND es.deleted_at IS NULL
              {tenantClause}
            GROUP BY es.id, es.location";

        await using var command = await CreateCommandAsync(sql, cancellationToken);
        AddParam(command, "pollutant_id", NpgsqlDbType.Uuid, pollutantId);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));
        AddTenantParam(command);

        return await ReadHeatmapAsync(command, cancellationToken);
    }

    // ─── Raw fallback path (p95) ────────────────────────────────────────────────

    private async Task<IReadOnlyList<TimeSeriesPoint>> GetTimeSeriesFromRawAsync(
        Guid pollutantId, Guid emissionSourceId, DateTime from, DateTime to,
        BucketWindow window, CancellationToken cancellationToken)
    {
        var bucketLiteral = BucketLiteral(window);
        var tenantClause = BuildTenantClause();

        var sql = $@"
            SELECT
                time_bucket(INTERVAL '{bucketLiteral}', rm.time) AS bucket_start,
                (percentile_cont(0.95) WITHIN GROUP (ORDER BY rm.raw_value))::numeric(18,6) AS value,
                COUNT(*)::int AS total_count,
                COUNT(*) FILTER (WHERE rm.quality = 0)::int AS valid_count
            FROM raw_measurement rm
            JOIN emission_source es ON es.id = rm.emission_source_id
            WHERE rm.pollutant_id = @pollutant_id
              AND rm.emission_source_id = @emission_source_id
              AND rm.time >= @from
              AND rm.time < @to
              AND es.deleted_at IS NULL
              {tenantClause}
            GROUP BY bucket_start
            ORDER BY bucket_start";

        await using var command = await CreateCommandAsync(sql, cancellationToken);
        AddParam(command, "pollutant_id", NpgsqlDbType.Uuid, pollutantId);
        AddParam(command, "emission_source_id", NpgsqlDbType.Uuid, emissionSourceId);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));
        AddTenantParam(command);

        return await ReadTimeSeriesAsync(command, cancellationToken);
    }

    private async Task<IReadOnlyList<HeatmapPoint>> GetHeatmapFromRawAsync(
        Guid pollutantId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var tenantClause = BuildTenantClause();

        var sql = $@"
            SELECT
                es.id AS emission_source_id,
                ST_Y(es.location) AS latitude,
                ST_X(es.location) AS longitude,
                (percentile_cont(0.95) WITHIN GROUP (ORDER BY rm.raw_value))::numeric(18,6) AS value,
                (array_agg(rm.unit_id))[1] AS unit_id,
                COUNT(*)::int AS total_count,
                COUNT(*) FILTER (WHERE rm.quality = 0)::int AS valid_count
            FROM raw_measurement rm
            JOIN emission_source es ON es.id = rm.emission_source_id
            WHERE rm.pollutant_id = @pollutant_id
              AND rm.time >= @from
              AND rm.time < @to
              AND es.deleted_at IS NULL
              {tenantClause}
            GROUP BY es.id, es.location";

        await using var command = await CreateCommandAsync(sql, cancellationToken);
        AddParam(command, "pollutant_id", NpgsqlDbType.Uuid, pollutantId);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));
        AddTenantParam(command);

        return await ReadHeatmapAsync(command, cancellationToken);
    }

    // ─── Result readers ────────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<TimeSeriesPoint>> ReadTimeSeriesAsync(
        NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var results = new List<TimeSeriesPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TimeSeriesPoint(
                BucketStart: DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                Value: reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                TotalPointsCount: reader.GetInt32(2),
                ValidPointsCount: reader.GetInt32(3)));
        }
        return results;
    }

    private static async Task<IReadOnlyList<HeatmapPoint>> ReadHeatmapAsync(
        NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var results = new List<HeatmapPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new HeatmapPoint(
                EmissionSourceId: reader.GetGuid(0),
                Latitude: reader.GetDouble(1),
                Longitude: reader.GetDouble(2),
                Value: reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                UnitId: reader.GetGuid(4),
                TotalPointsCount: reader.GetInt32(5),
                ValidPointsCount: reader.GetInt32(6)));
        }
        return results;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private async Task<NpgsqlCommand> CreateCommandAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
        return new NpgsqlCommand(sql, connection);
    }

    private string BuildTenantClause() =>
        context.BypassTenantFilter ? string.Empty : "AND es.enterprise_id = @tenant_id";

    private void AddTenantParam(NpgsqlCommand command)
    {
        if (context.BypassTenantFilter || context.TenantFilterId is null) return;
        AddParam(command, "tenant_id", NpgsqlDbType.Uuid, context.TenantFilterId.Value);
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

    private static string BucketLiteral(BucketWindow window) => window switch
    {
        BucketWindow.Minute1 => "1 minute",
        BucketWindow.Minute5 => "5 minutes",
        BucketWindow.Minute15 => "15 minutes",
        BucketWindow.Minute30 => "30 minutes",
        BucketWindow.Hour1 => "1 hour",
        BucketWindow.Hour6 => "6 hours",
        BucketWindow.Day1 => "1 day",
        _ => throw new ArgumentOutOfRangeException(nameof(window), window, null)
    };

    private static string CaAggregationExpression(AggregationFunc agg) => agg switch
    {
        AggregationFunc.Average => "SUM(m.sum_value) / NULLIF(SUM(m.sample_count), 0)",
        AggregationFunc.Max => "MAX(m.max_value)",
        AggregationFunc.Min => "MIN(m.min_value)",
        AggregationFunc.Sum => "SUM(m.sum_value)",
        _ => throw new ArgumentOutOfRangeException(nameof(agg), agg, null)
    };
}
