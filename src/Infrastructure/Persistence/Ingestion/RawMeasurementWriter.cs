using Application.Common.Interfaces.Ingestion;
using Domain.Entities.Monitoring;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence.Ingestion;

internal class RawMeasurementWriter(NpgsqlDataSource dataSource) : IRawMeasurementWriter
{
    public async Task<int> WriteBatchAsync(IEnumerable<RawMeasurement> measurements,
        CancellationToken cancellationToken)
    {
        var batch = measurements as RawMeasurement[] ?? measurements.ToArray();
        if (batch.Length == 0) return 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY raw_measurement (time, emission_source_id, pollutant_id, device_id, unit_id, raw_value, quality) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        foreach (var m in batch)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(m.Time, NpgsqlDbType.TimestampTz, cancellationToken);
            await writer.WriteAsync(m.EmissionSourceId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(m.PollutantId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(m.DeviceId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(m.UnitId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(m.RawValue, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync((int)m.Quality, NpgsqlDbType.Integer, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
        return batch.Length;
    }
}
