using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MeasurementAndComplianceRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exceedance_event");

            migrationBuilder.DropIndex(
                name: "ix_measurement_emission_source_id_pollutant_id_timestamp",
                table: "measurement");

            migrationBuilder.RenameColumn(
                name: "timestamp",
                table: "measurement",
                newName: "window_start");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "measurement",
                newName: "window");

            migrationBuilder.RenameColumn(
                name: "reason",
                table: "measurement",
                newName: "substitution_reason");

            migrationBuilder.RenameColumn(
                name: "period",
                table: "measurement",
                newName: "valid_points_count");

            migrationBuilder.AlterColumn<decimal>(
                name: "value",
                table: "measurement",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<int>(
                name: "aggregation",
                table: "measurement",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "expected_points_count",
                table: "measurement",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "normalized_value",
                table: "measurement",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quality",
                table: "measurement",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "quality_note",
                table: "measurement",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "substituted_at",
                table: "measurement",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "substituted_by",
                table: "measurement",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "uncertainty",
                table: "measurement",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "window_end",
                table: "measurement",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<decimal>(
                name: "value",
                table: "emission_limit",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<DateTime>(
                name: "valid_from",
                table: "emission_limit",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "emission_source_id",
                table: "emission_limit",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "installation_id",
                table: "emission_limit",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "limit_type",
                table: "emission_limit",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "compliance_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<int>(type: "integer", nullable: false),
                    emission_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    measurement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    limit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    window_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    window_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ratio = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_compliance_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_compliance_event_emission_limit_limit_id",
                        column: x => x.limit_id,
                        principalTable: "emission_limit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_compliance_event_emission_source_emission_source_id",
                        column: x => x.emission_source_id,
                        principalTable: "emission_source",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_compliance_event_measurement_measurement_id",
                        column: x => x.measurement_id,
                        principalTable: "measurement",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_compliance_event_monitoring_device_device_id",
                        column: x => x.device_id,
                        principalTable: "monitoring_device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "raw_measurement",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    emission_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pollutant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    raw_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    quality = table.Column<int>(type: "integer", nullable: false),
                    ingestion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_raw_measurement", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "raw_process_parameter",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    emission_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter_type = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quality = table.Column<int>(type: "integer", nullable: false),
                    ingestion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_raw_process_parameter", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_measurement_emission_source_id_pollutant_id_window_end_wind",
                table: "measurement",
                columns: new[] { "emission_source_id", "pollutant_id", "window_end", "window", "aggregation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_emission_limit_installation_id",
                table: "emission_limit",
                column: "installation_id");

            migrationBuilder.CreateIndex(
                name: "ix_compliance_event_device_id",
                table: "compliance_event",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_compliance_event_emission_source_id_detected_at",
                table: "compliance_event",
                columns: new[] { "emission_source_id", "detected_at" });

            migrationBuilder.CreateIndex(
                name: "ix_compliance_event_limit_id",
                table: "compliance_event",
                column: "limit_id");

            migrationBuilder.CreateIndex(
                name: "ix_compliance_event_measurement_id",
                table: "compliance_event",
                column: "measurement_id");

            migrationBuilder.CreateIndex(
                name: "ix_raw_measurement_emission_source_id_pollutant_id_time",
                table: "raw_measurement",
                columns: new[] { "emission_source_id", "pollutant_id", "time" });

            migrationBuilder.CreateIndex(
                name: "ix_raw_measurement_time",
                table: "raw_measurement",
                column: "time");

            migrationBuilder.CreateIndex(
                name: "ix_raw_process_parameter_emission_source_id_parameter_type_time",
                table: "raw_process_parameter",
                columns: new[] { "emission_source_id", "parameter_type", "time" });

            migrationBuilder.CreateIndex(
                name: "ix_raw_process_parameter_time",
                table: "raw_process_parameter",
                column: "time");

            migrationBuilder.AddForeignKey(
                name: "fk_emission_limit_installation_installation_id",
                table: "emission_limit",
                column: "installation_id",
                principalTable: "installation",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_emission_limit_installation_installation_id",
                table: "emission_limit");

            migrationBuilder.DropTable(
                name: "compliance_event");

            migrationBuilder.DropTable(
                name: "raw_measurement");

            migrationBuilder.DropTable(
                name: "raw_process_parameter");

            migrationBuilder.DropIndex(
                name: "ix_measurement_emission_source_id_pollutant_id_window_end_wind",
                table: "measurement");

            migrationBuilder.DropIndex(
                name: "ix_emission_limit_installation_id",
                table: "emission_limit");

            migrationBuilder.DropColumn(
                name: "aggregation",
                table: "measurement");

            migrationBuilder.DropColumn(
                name: "expected_points_count",
                table: "measurement");

            migrationBuilder.DropColumn(
                name: "normalized_value",
                table: "measurement");

            migrationBuilder.DropColumn(
                name: "quality",
                table: "measurement");

            migrationBuilder.DropColumn(
                name: "quality_note",
                table: "measurement");

            migrationBuilder.DropColumn(
                name: "substituted_at",
                table: "measurement");

            migrationBuilder.DropColumn(
                name: "substituted_by",
                table: "measurement");

            migrationBuilder.DropColumn(
                name: "uncertainty",
                table: "measurement");

            migrationBuilder.DropColumn(
                name: "window_end",
                table: "measurement");

            migrationBuilder.DropColumn(
                name: "installation_id",
                table: "emission_limit");

            migrationBuilder.DropColumn(
                name: "limit_type",
                table: "emission_limit");

            migrationBuilder.RenameColumn(
                name: "window_start",
                table: "measurement",
                newName: "timestamp");

            migrationBuilder.RenameColumn(
                name: "window",
                table: "measurement",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "valid_points_count",
                table: "measurement",
                newName: "period");

            migrationBuilder.RenameColumn(
                name: "substitution_reason",
                table: "measurement",
                newName: "reason");

            migrationBuilder.AlterColumn<decimal>(
                name: "value",
                table: "measurement",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "value",
                table: "emission_limit",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<DateTime>(
                name: "valid_from",
                table: "emission_limit",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "emission_source_id",
                table: "emission_limit",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "exceedance_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    limit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    measurement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    magnitude = table.Column<decimal>(type: "numeric", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exceedance_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_exceedance_event_emission_limit_limit_id",
                        column: x => x.limit_id,
                        principalTable: "emission_limit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exceedance_event_measurement_measurement_id",
                        column: x => x.measurement_id,
                        principalTable: "measurement",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_measurement_emission_source_id_pollutant_id_timestamp",
                table: "measurement",
                columns: new[] { "emission_source_id", "pollutant_id", "timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exceedance_event_limit_id",
                table: "exceedance_event",
                column: "limit_id");

            migrationBuilder.CreateIndex(
                name: "ix_exceedance_event_measurement_id",
                table: "exceedance_event",
                column: "measurement_id");
        }
    }
}
