using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompliancePerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // measurement_1m is a Timescale continuous aggregate (see migration
            // 20260514183814) and has no EF entity to attach a HasIndex to — raw SQL is the
            // only path. The existing CA index (pollutant_id, bucket DESC) covers pollutant
            // scans but not the per-source filter that the materializer
            // (GetReBucketedBulkAsync, 5-min cadence) and AnnualLoad
            // (GetRollingAverageRateAsync, daily) actually run. Leading source_id lets the
            // planner skip straight to the matching subset before applying the time-bucket
            // range.
            //
            // Write cost is acceptable — measurement_1m is populated by the 5-min refresh
            // policy in small batches, not by client INSERTs. The same approach was
            // deliberately NOT applied to raw_measurement (the hot ingest hypertable) — adding
            // a (device_id, time) index there would amplify INSERT work without enough read
            // benefit now that GetDeviceLastSeenAsync has a time bound (commit e4f3d0d).
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_measurement_1m_source_pollutant_bucket
                ON measurement_1m (emission_source_id, pollutant_id, bucket DESC);",
                suppressTransaction: true);

            migrationBuilder.CreateIndex(
                name: "ix_compliance_event_open_by_type",
                table: "compliance_event",
                columns: new[] { "event_type", "emission_source_id" },
                filter: "status = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_compliance_event_open_by_type",
                table: "compliance_event");

            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_measurement_1m_source_pollutant_bucket;",
                suppressTransaction: true);
        }
    }
}
