using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnrichPollutantsAndDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "monitoring_device",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE monitoring_device SET status = CASE WHEN is_online THEN 0 ELSE 1 END;");

            migrationBuilder.DropColumn(
                name: "is_online",
                table: "monitoring_device");

            migrationBuilder.AddColumn<string>(
                name: "cas_number",
                table: "pollutant",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "category",
                table: "pollutant",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "default_dimension",
                table: "pollutant",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "default_o2reference",
                table: "pollutant",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "eprtr_threshold_kg_year",
                table: "pollutant",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "media",
                table: "pollutant",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "calibration_record",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    performed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_due_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    result = table.Column<int>(type: "integer", nullable: false),
                    performed_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    certificate_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calibration_record", x => x.id);
                    table.ForeignKey(
                        name: "fk_calibration_record_monitoring_device_device_id",
                        column: x => x.device_id,
                        principalTable: "monitoring_device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_pollutant_capability",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pollutant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    range_min = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    range_max = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    range_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accuracy_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_pollutant_capability", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_pollutant_capability_measure_unit_range_unit_id",
                        column: x => x.range_unit_id,
                        principalTable: "measure_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_device_pollutant_capability_monitoring_device_device_id",
                        column: x => x.device_id,
                        principalTable: "monitoring_device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_device_pollutant_capability_pollutant_pollutant_id",
                        column: x => x.pollutant_id,
                        principalTable: "pollutant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pollutant_cas_number",
                table: "pollutant",
                column: "cas_number",
                unique: true,
                filter: "cas_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_calibration_record_device_id_performed_at",
                table: "calibration_record",
                columns: new[] { "device_id", "performed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_device_pollutant_capability_device_id_pollutant_id",
                table: "device_pollutant_capability",
                columns: new[] { "device_id", "pollutant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_pollutant_capability_pollutant_id",
                table: "device_pollutant_capability",
                column: "pollutant_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_pollutant_capability_range_unit_id",
                table: "device_pollutant_capability",
                column: "range_unit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calibration_record");

            migrationBuilder.DropTable(
                name: "device_pollutant_capability");

            migrationBuilder.DropIndex(
                name: "ix_pollutant_cas_number",
                table: "pollutant");

            migrationBuilder.DropColumn(
                name: "cas_number",
                table: "pollutant");

            migrationBuilder.DropColumn(
                name: "category",
                table: "pollutant");

            migrationBuilder.DropColumn(
                name: "default_dimension",
                table: "pollutant");

            migrationBuilder.DropColumn(
                name: "default_o2reference",
                table: "pollutant");

            migrationBuilder.DropColumn(
                name: "eprtr_threshold_kg_year",
                table: "pollutant");

            migrationBuilder.DropColumn(
                name: "media",
                table: "pollutant");

            migrationBuilder.DropColumn(
                name: "status",
                table: "monitoring_device");

            migrationBuilder.AddColumn<bool>(
                name: "is_online",
                table: "monitoring_device",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
