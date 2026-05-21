using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPollutantCanonicalUnitAndMolarMass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Two-step add for the required FK so existing rows don't violate the constraint:
            // 1) add nullable column, 2) backfill from measure_unit by matching dimension,
            // 3) flip to NOT NULL, 4) install index + FK.
            migrationBuilder.AddColumn<Guid>(
                name: "canonical_unit_id",
                table: "pollutant",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "molar_mass",
                table: "pollutant",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            // Backfill: pick the first measure_unit whose dimension matches the pollutant's
            // default_dimension. For the built-in seed that lands on mg/m³ for MassConcentration
            // pollutants, kg/h for MassFlow, etc. No-op on a fresh DB (both tables empty).
            migrationBuilder.Sql(@"
                UPDATE pollutant p
                SET canonical_unit_id = (
                    SELECT u.id
                    FROM measure_unit u
                    WHERE u.dimension = p.default_dimension
                      AND u.deleted_at IS NULL
                    ORDER BY u.to_base_factor ASC, u.created_at ASC
                    LIMIT 1
                )
                WHERE p.canonical_unit_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "canonical_unit_id",
                table: "pollutant",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pollutant_canonical_unit_id",
                table: "pollutant",
                column: "canonical_unit_id");

            migrationBuilder.AddForeignKey(
                name: "fk_pollutant_measure_unit_canonical_unit_id",
                table: "pollutant",
                column: "canonical_unit_id",
                principalTable: "measure_unit",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pollutant_measure_unit_canonical_unit_id",
                table: "pollutant");

            migrationBuilder.DropIndex(
                name: "ix_pollutant_canonical_unit_id",
                table: "pollutant");

            migrationBuilder.DropColumn(
                name: "canonical_unit_id",
                table: "pollutant");

            migrationBuilder.DropColumn(
                name: "molar_mass",
                table: "pollutant");
        }
    }
}
