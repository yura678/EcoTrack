using Application.Common.Interfaces.Ingestion;
using Domain.Entities.Monitoring;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence.Ingestion;

internal class RawProcessParameterWriter(NpgsqlDataSource dataSource) : IRawProcessParameterWriter
{
    public async Task<int> WriteBatchAsync(IEnumerable<RawProcessParameter> parameters,
        CancellationToken cancellationToken)
    {
        var batch = parameters as RawProcessParameter[] ?? parameters.ToArray();
        if (batch.Length == 0) return 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY raw_process_parameter (time, emission_source_id, device_id, parameter_type, value, unit_id, quality) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        foreach (var p in batch)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(p.Time, NpgsqlDbType.TimestampTz, cancellationToken);
            await writer.WriteAsync(p.EmissionSourceId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(p.DeviceId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync((int)p.ParameterType, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(p.Value, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(p.UnitId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync((int)p.Quality, NpgsqlDbType.Integer, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
        return batch.Length;
    }
}
