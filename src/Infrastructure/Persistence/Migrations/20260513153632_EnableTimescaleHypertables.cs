using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableTimescaleHypertables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS timescaledb;");

            // Timescale requires that every unique index on a hypertable include the
            // partitioning column. The previous migration created PK on `id` alone, so
            // we drop it and recreate as composite (id, time) before conversion.
            migrationBuilder.Sql(@"
                ALTER TABLE raw_measurement DROP CONSTRAINT IF EXISTS pk_raw_measurement;
                ALTER TABLE raw_measurement ADD CONSTRAINT pk_raw_measurement PRIMARY KEY (id, time);");

            migrationBuilder.Sql(@"
                ALTER TABLE raw_process_parameter DROP CONSTRAINT IF EXISTS pk_raw_process_parameter;
                ALTER TABLE raw_process_parameter ADD CONSTRAINT pk_raw_process_parameter PRIMARY KEY (id, time);");

            // Convert raw_measurement to a hypertable partitioned on `time`.
            // Tables are expected to be empty at this point. if_not_exists guards re-runs.
            migrationBuilder.Sql(@"
                SELECT create_hypertable('raw_measurement', 'time',
                    chunk_time_interval => INTERVAL '7 days',
                    if_not_exists => TRUE,
                    migrate_data => TRUE);");

            migrationBuilder.Sql(@"
                SELECT create_hypertable('raw_process_parameter', 'time',
                    chunk_time_interval => INTERVAL '7 days',
                    if_not_exists => TRUE,
                    migrate_data => TRUE);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverting hypertable to a regular table is destructive and rarely needed in dev.
            // No-op; if you need to roll back, drop the tables and re-run earlier migrations.
        }
    }
}
