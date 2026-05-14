using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeasurement1mContinuousAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Continuous aggregate at 1-minute granularity over raw_measurement.
            // Stores building blocks (sum, count, min, max) so larger windows can be
            // re-aggregated correctly at query time: avg = SUM(sum)/SUM(count).
            // Cannot run inside a transaction — Timescale requires it standalone.
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

            // Refresh policy: every 5 minutes, refresh the window from 2 hours ago up to
            // 1 minute ago. The 1-minute lag avoids partial-bucket churn on the leading edge.
            migrationBuilder.Sql(@"
                SELECT add_continuous_aggregate_policy('measurement_1m',
                    start_offset => INTERVAL '2 hours',
                    end_offset   => INTERVAL '1 minute',
                    schedule_interval => INTERVAL '5 minutes');",
                suppressTransaction: true);

            // Index to speed up GROUP BY emission_source on heatmap queries.
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
        }
    }
}
