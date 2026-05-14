using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMonitoringPlanAndRequirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "monitoring_requirement");

            migrationBuilder.DropTable(
                name: "monitoring_plan");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "monitoring_plan",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monitoring_plan", x => x.id);
                    table.ForeignKey(
                        name: "fk_monitoring_plan_installation_installation_id",
                        column: x => x.installation_id,
                        principalTable: "installation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "monitoring_requirement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    monitoring_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pollutant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frequency = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monitoring_requirement", x => x.id);
                    table.ForeignKey(
                        name: "fk_monitoring_requirement_emission_source_emission_source_id",
                        column: x => x.emission_source_id,
                        principalTable: "emission_source",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_monitoring_requirement_monitoring_plan_monitoring_plan_id",
                        column: x => x.monitoring_plan_id,
                        principalTable: "monitoring_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_monitoring_requirement_pollutant_pollutant_id",
                        column: x => x.pollutant_id,
                        principalTable: "pollutant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_plan_installation_id",
                table: "monitoring_plan",
                column: "installation_id");

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_requirement_emission_source_id",
                table: "monitoring_requirement",
                column: "emission_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_requirement_monitoring_plan_id_emission_source_i",
                table: "monitoring_requirement",
                columns: new[] { "monitoring_plan_id", "emission_source_id", "pollutant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_requirement_pollutant_id",
                table: "monitoring_requirement",
                column: "pollutant_id");
        }
    }
}
