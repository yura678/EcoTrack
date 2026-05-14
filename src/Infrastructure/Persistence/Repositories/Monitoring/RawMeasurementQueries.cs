using Application.Common.Interfaces.Queries.Monitoring;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class RawMeasurementQueries(ApplicationDbContext context) : IRawMeasurementQueries
{
    public async Task<IReadOnlyList<TimeSeriesPoint>> GetTimeSeriesAsync(
        Guid pollutantId,
        Guid emissionSourceId,
        DateTime from,
        DateTime to,
        BucketWindow window,
        AggregationFunc aggregation,
        CancellationToken cancellationToken)
    {
        var bucketLiteral = BucketLiteral(window);
        var aggExpr = AggregationExpression(aggregation, "rm.raw_value");
        var tenantClause = BuildTenantClause();

        var sql = $@"
            SELECT
                time_bucket(interval '{bucketLiteral}', rm.time) AS bucket_start,
                {aggExpr}::numeric(18,6) AS value,
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

        var results = new List<TimeSeriesPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TimeSeriesPoint(
                BucketStart: DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                Value: reader.GetDecimal(1),
                TotalPointsCount: reader.GetInt32(2),
                ValidPointsCount: reader.GetInt32(3)));
        }
        return results;
    }

    public async Task<IReadOnlyList<HeatmapPoint>> GetHeatmapAsync(
        Guid pollutantId,
        DateTime from,
        DateTime to,
        AggregationFunc aggregation,
        CancellationToken cancellationToken)
    {
        var aggExpr = AggregationExpression(aggregation, "rm.raw_value");
        var tenantClause = BuildTenantClause();

        var sql = $@"
            SELECT
                es.id AS emission_source_id,
                ST_Y(es.location) AS latitude,
                ST_X(es.location) AS longitude,
                {aggExpr}::numeric(18,6) AS value,
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

        var results = new List<HeatmapPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new HeatmapPoint(
                EmissionSourceId: reader.GetGuid(0),
                Latitude: reader.GetDouble(1),
                Longitude: reader.GetDouble(2),
                Value: reader.GetDecimal(3),
                UnitId: reader.GetGuid(4),
                TotalPointsCount: reader.GetInt32(5),
                ValidPointsCount: reader.GetInt32(6)));
        }
        return results;
    }

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

    private static string AggregationExpression(AggregationFunc agg, string column) => agg switch
    {
        AggregationFunc.Average => $"AVG({column})",
        AggregationFunc.Max => $"MAX({column})",
        AggregationFunc.Min => $"MIN({column})",
        AggregationFunc.Sum => $"SUM({column})",
        AggregationFunc.P95 => $"percentile_cont(0.95) WITHIN GROUP (ORDER BY {column})",
        _ => throw new ArgumentOutOfRangeException(nameof(agg), agg, null)
    };
}
