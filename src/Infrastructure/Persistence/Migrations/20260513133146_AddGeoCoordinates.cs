using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGeoCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AddColumn<double>(
                name: "elevation",
                table: "site",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "location",
                table: "site",
                type: "geometry(Point, 4326)",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "location",
                table: "emission_source",
                type: "geometry(Point, 4326)",
                nullable: false,
                defaultValueSql: "ST_SetSRID(ST_MakePoint(0, 0), 4326)");

            migrationBuilder.CreateIndex(
                name: "ix_site_location",
                table: "site",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_emission_source_location",
                table: "emission_source",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_site_location",
                table: "site");

            migrationBuilder.DropIndex(
                name: "ix_emission_source_location",
                table: "emission_source");

            migrationBuilder.DropColumn(
                name: "elevation",
                table: "site");

            migrationBuilder.DropColumn(
                name: "location",
                table: "site");

            migrationBuilder.DropColumn(
                name: "location",
                table: "emission_source");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
