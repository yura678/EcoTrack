using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncRawTablePkComposite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op. The actual primary-key swap to (id, time) happens inside
            // EnableTimescaleHypertables migration (raw SQL ALTER TABLE), because it
            // must run before create_hypertable. This migration exists solely to
            // synchronize the EF model snapshot with the composite-PK domain config.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
