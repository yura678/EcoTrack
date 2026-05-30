using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.Monitoring;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class RawProcessParameterQueries(ApplicationDbContext context) : IRawProcessParameterQueries
{
    public async Task<IReadOnlyList<ProcessParameterPoint>> GetTimeSeriesAsync(
        Guid emissionSourceId,
        ParameterType parameterType,
        DateTime from,
        DateTime to,
        BucketWindow window,
        CancellationToken cancellationToken)
    {
        var bucketLiteral = BucketLiteral(window);
        var tenantClause = BuildTenantClause();

        var sql = $@"
            SELECT
                time_bucket(INTERVAL '{bucketLiteral}', m.bucket) AS bucket_start,
                (SUM(m.valid_sum) / NULLIF(SUM(m.valid_count), 0))::numeric(18,6) AS value,
                (array_agg(m.unit_id))[1] AS unit_id,
                COALESCE(SUM(m.sample_count), 0)::int AS total_count,
                COALESCE(SUM(m.valid_count), 0)::int AS valid_count
            FROM process_parameter_1m m
            JOIN emission_source es ON es.id = m.emission_source_id
            WHERE m.emission_source_id = @source_id
              AND m.parameter_type = @parameter_type
              AND m.bucket >= @from
              AND m.bucket < @to
              AND es.deleted_at IS NULL
              {tenantClause}
            GROUP BY bucket_start
            ORDER BY bucket_start";

        await using var command = await CreateCommandAsync(sql, cancellationToken);
        AddParam(command, "source_id", NpgsqlDbType.Uuid, emissionSourceId);
        AddParam(command, "parameter_type", NpgsqlDbType.Integer, (int)parameterType);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));
        AddTenantParam(command);

        var results = new List<ProcessParameterPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ProcessParameterPoint(
                BucketStart: DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                Value: reader.IsDBNull(1) ? (decimal?)null : reader.GetDecimal(1),
                UnitId: reader.GetGuid(2),
                TotalPointsCount: reader.GetInt32(3),
                ValidPointsCount: reader.GetInt32(4)));
        }
        return results;
    }

    public async Task<IReadOnlyList<ProcessParameterLatest>> GetLatestForSourceAsync(
        Guid emissionSourceId,
        IReadOnlyCollection<ParameterType>? parameterTypes,
        CancellationToken cancellationToken)
    {
        var tenantClause = BuildTenantClause();
        var typeFilter = parameterTypes is { Count: > 0 }
            ? "AND rp.parameter_type = ANY(@parameter_types)"
            : string.Empty;

        var sql = $@"
            SELECT DISTINCT ON (rp.parameter_type)
                rp.parameter_type,
                rp.time,
                rp.value,
                rp.unit_id
            FROM raw_process_parameter rp
            JOIN emission_source es ON es.id = rp.emission_source_id
            WHERE rp.emission_source_id = @source_id
              {typeFilter}
              AND es.deleted_at IS NULL
              {tenantClause}
            ORDER BY rp.parameter_type, rp.time DESC";

        await using var command = await CreateCommandAsync(sql, cancellationToken);
        AddParam(command, "source_id", NpgsqlDbType.Uuid, emissionSourceId);
        if (parameterTypes is { Count: > 0 })
        {
            AddParam(command, "parameter_types", NpgsqlDbType.Array | NpgsqlDbType.Integer,
                parameterTypes.Select(t => (int)t).ToArray());
        }
        AddTenantParam(command);

        var results = new List<ProcessParameterLatest>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ProcessParameterLatest(
                EmissionSourceId: emissionSourceId,
                ParameterType: (ParameterType)reader.GetInt32(0),
                LastSeenAt: DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc),
                Value: reader.GetDecimal(2),
                UnitId: reader.GetGuid(3)));
        }
        return results;
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<NpgsqlCommand> CreateCommandAsync(string sql, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
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
        BucketWindow.Minute10 => "10 minutes",
        BucketWindow.Minute15 => "15 minutes",
        BucketWindow.Minute30 => "30 minutes",
        BucketWindow.HalfHour => "30 minutes",
        BucketWindow.Hour1 => "1 hour",
        BucketWindow.Hour6 => "6 hours",
        BucketWindow.Hour24 => "24 hours",
        BucketWindow.Day1 => "1 day",
        BucketWindow.Month1 => "1 month",
        BucketWindow.Year1 => "1 year",
        _ => throw new ArgumentOutOfRangeException(nameof(window), window, null)
    };
}
