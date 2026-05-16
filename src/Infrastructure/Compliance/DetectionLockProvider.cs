using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Compliance;

/// <summary>
/// Acquires a Postgres advisory lock so only one instance runs compliance detection at a time.
/// The lock is held on a dedicated connection for the full tick and released on Dispose.
/// </summary>
public class DetectionLockProvider(
    NpgsqlDataSource dataSource,
    ILogger<DetectionLockProvider> logger)
{
    // Arbitrary constant — must be stable across instances. Different services should use different keys.
    private const long LockKey = 0x4543_4F44_4554_4543; // "ECODETEC"

    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
            cmd.Parameters.AddWithValue("@key", LockKey);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            var acquired = result is bool b && b;

            if (!acquired)
            {
                await connection.DisposeAsync();
                return null;
            }

            return new LockHandle(connection, LockKey, logger);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class LockHandle(NpgsqlConnection connection, long key, ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
                cmd.Parameters.AddWithValue("@key", key);
                await cmd.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to release detection advisory lock.");
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
