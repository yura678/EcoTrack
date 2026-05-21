using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitIdToMeasurement1mGroupBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adds unit_id to GROUP BY so a minute that received raw rows in two units
            // (e.g. mg/m³ and µg/m³ from a swapped device) produces two CA rows instead of
            // one fictitious row with an arbitrary unit_id. The materializer fans these per-unit
            // rows back together after converting each to the pollutant's canonical unit, so
            // downstream Measurement.Value is honest across device generations.
            //
            // Timescale continuous aggregates don't support ALTER VIEW ... ADD COLUMN or
            // GROUP BY changes, so we drop and recreate. Historical data is lost; the refresh
            // policy + SeedRawMeasurementsAsync rebuild what is needed.
            migrationBuilder.Sql(
                "DROP MATERIALIZED VIEW IF EXISTS measurement_1m CASCADE;",
                suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE MATERIALIZED VIEW measurement_1m
                WITH (timescaledb.continuous) AS
                SELECT
                    emission_source_id,
                    pollutant_id,
                    unit_id,
                    time_bucket(INTERVAL '1 minute', time) AS bucket,
                    SUM(raw_value) AS sum_value,
                    SUM(raw_value) FILTER (WHERE quality = 0) AS valid_sum,
                    MIN(raw_value) AS min_value,
                    MAX(raw_value) AS max_value,
                    COUNT(*) AS sample_count,
                    COUNT(*) FILTER (WHERE quality = 0) AS valid_count
                FROM raw_measurement
                GROUP BY emission_source_id, pollutant_id, unit_id, bucket
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

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_measurement_1m_source_pollutant_bucket
                ON measurement_1m (emission_source_id, pollutant_id, bucket DESC);",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the pre-Phase-2 shape: unit_id collapsed via array_agg, no per-unit row split.
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

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_measurement_1m_source_pollutant_bucket
                ON measurement_1m (emission_source_id, pollutant_id, bucket DESC);",
                suppressTransaction: true);
        }
    }
}
