using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessParameter1mCa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1-minute CA over raw_process_parameter — parallel to measurement_1m.
            // Grouped by (source, parameter_type) so each kind of stack reading aggregates
            // independently. valid_sum lets downstream consumers compute valid-only averages
            // (needed e.g. by O2 normalization to ignore offline/maintenance samples).
            migrationBuilder.Sql(@"
                CREATE MATERIALIZED VIEW process_parameter_1m
                WITH (timescaledb.continuous) AS
                SELECT
                    emission_source_id,
                    parameter_type,
                    time_bucket(INTERVAL '1 minute', time) AS bucket,
                    SUM(value) AS sum_value,
                    SUM(value) FILTER (WHERE quality = 0) AS valid_sum,
                    MIN(value) AS min_value,
                    MAX(value) AS max_value,
                    COUNT(*) AS sample_count,
                    COUNT(*) FILTER (WHERE quality = 0) AS valid_count,
                    (array_agg(unit_id))[1] AS unit_id
                FROM raw_process_parameter
                GROUP BY emission_source_id, parameter_type, bucket
                WITH NO DATA;",
                suppressTransaction: true);

            migrationBuilder.Sql(@"
                SELECT add_continuous_aggregate_policy('process_parameter_1m',
                    start_offset => INTERVAL '2 hours',
                    end_offset   => INTERVAL '1 minute',
                    schedule_interval => INTERVAL '5 minutes');",
                suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_process_parameter_1m_source_type_bucket
                ON process_parameter_1m (emission_source_id, parameter_type, bucket DESC);",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP MATERIALIZED VIEW IF EXISTS process_parameter_1m CASCADE;",
                suppressTransaction: true);
        }
    }
}
