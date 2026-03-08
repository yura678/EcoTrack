using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class deleteRoleClaimEnterpriseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_role_claims_enterprise_enterprise_id",
                schema: "usr",
                table: "role-claims");

            migrationBuilder.DropIndex(
                name: "ix_role_claims_enterprise_id",
                schema: "usr",
                table: "role-claims");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                schema: "usr",
                table: "role-claims");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                schema: "usr",
                table: "role-claims",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_role_claims_enterprise_id",
                schema: "usr",
                table: "role-claims",
                column: "enterprise_id");

            migrationBuilder.AddForeignKey(
                name: "fk_role_claims_enterprise_enterprise_id",
                schema: "usr",
                table: "role-claims",
                column: "enterprise_id",
                principalTable: "enterprise",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
