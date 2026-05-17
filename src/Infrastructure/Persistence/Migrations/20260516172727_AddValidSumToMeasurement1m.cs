using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddValidSumToMeasurement1m : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add valid_sum = SUM of raw_value over valid (Quality=0) points only.
            // Continuous aggregates in Timescale don't support ALTER VIEW ... ADD COLUMN,
            // so we drop and recreate. Historical materialised data is lost; the refresh
            // policy backfills the most recent 2h, and SeedRawMeasurementsAsync does a full
            // manual refresh on dev startup. In production, callers using valid_sum will
            // initially see only real-time aggregated data until the next manual or scheduled
            // refresh cycle covers older chunks.
            migrationBuilder.Sql(
                "DROP MATERIALIZED VIEW IF EXISTS measurement_1m CASCADE;",
                suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE MATERIALIZED VIEW measurement_1m
                WITH (timescaledb.continuous) AS
                SELECT
                    emission_source_id,
                    pollutant_id,
                    time_bucket(INTERVAL '1 minute', time) AS bucket,
                    SUM(raw_value) AS sum_value,
                    SUM(raw_value) FILTER (WHERE quality = 0) AS valid_sum,
                    MIN(raw_value) AS min_value,
                    MAX(raw_value) AS max_value,
                    COUNT(*) AS sample_count,
                    COUNT(*) FILTER (WHERE quality = 0) AS valid_count,
                    (array_agg(unit_id))[1] AS unit_id
                FROM raw_measurement
                GROUP BY emission_source_id, pollutant_id, bucket
                WITH NO DATA;",
                suppressTransaction: true);

            migrationBuilder.Sql(@"
                SELECT add_continuous_aggregate_policy('measurement_1m',
                    start_offset => INTERVAL '2 hours',
                    end_offset   => INTERVAL '1 minute',
                    schedule_interval => INTERVAL '5 minutes');",
                suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_measurement_1m_pollutant_bucket
                ON measurement_1m (pollutant_id, bucket DESC);",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP MATERIALIZED VIEW IF EXISTS measurement_1m CASCADE;",
                suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE MATERIALIZED VIEW measurement_1m
                WITH (timescaledb.continuous) AS
                SELECT
                    emission_source_id,
                    pollutant_id,
                    time_bucket(INTERVAL '1 minute', time) AS bucket,
                    SUM(raw_value) AS sum_value,
                    MIN(raw_value) AS min_value,
                    MAX(raw_value) AS max_value,
                    COUNT(*) AS sample_count,
                    COUNT(*) FILTER (WHERE quality = 0) AS valid_count,
                    (array_agg(unit_id))[1] AS unit_id
                FROM raw_measurement
                GROUP BY emission_source_id, pollutant_id, bucket
                WITH NO DATA;",
                suppressTransaction: true);

            migrationBuilder.Sql(@"
                SELECT add_continuous_aggregate_policy('measurement_1m',
                    start_offset => INTERVAL '2 hours',
                    end_offset   => INTERVAL '1 minute',
                    schedule_interval => INTERVAL '5 minutes');",
                suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_measurement_1m_pollutant_bucket
                ON measurement_1m (pollutant_id, bucket DESC);",
                suppressTransaction: true);
        }
    }
}
