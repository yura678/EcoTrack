using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "RoleNameIndex",
                schema: "usr",
                table: "roles");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "usr",
                table: "roles",
                columns: new[] { "normalized_name", "enterprise_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "RoleNameIndex",
                schema: "usr",
                table: "roles");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "usr",
                table: "roles",
                column: "normalized_name",
                unique: true);
        }
    }
}
