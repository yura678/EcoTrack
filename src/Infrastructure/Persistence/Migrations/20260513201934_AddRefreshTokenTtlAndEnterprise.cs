using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenTtlAndEnterprise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_refresh_tokens_user_id",
                schema: "usr",
                table: "user-refresh-tokens");

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                schema: "usr",
                table: "user-refresh-tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at",
                schema: "usr",
                table: "user-refresh-tokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "ix_user_refresh_tokens_user_id_is_valid",
                schema: "usr",
                table: "user-refresh-tokens",
                columns: new[] { "user_id", "is_valid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_refresh_tokens_user_id_is_valid",
                schema: "usr",
                table: "user-refresh-tokens");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                schema: "usr",
                table: "user-refresh-tokens");

            migrationBuilder.DropColumn(
                name: "expires_at",
                schema: "usr",
                table: "user-refresh-tokens");

            migrationBuilder.CreateIndex(
                name: "ix_user_refresh_tokens_user_id",
                schema: "usr",
                table: "user-refresh-tokens",
                column: "user_id");
        }
    }
}
