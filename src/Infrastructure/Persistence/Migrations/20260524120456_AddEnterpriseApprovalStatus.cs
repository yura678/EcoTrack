using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "approval_decision_at",
                table: "enterprise",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approval_decision_by_user_id",
                table: "enterprise",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "enterprise",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "enterprise",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "ix_enterprise_status_created",
                table: "enterprise",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_enterprise_status_created",
                table: "enterprise");

            migrationBuilder.DropColumn(
                name: "approval_decision_at",
                table: "enterprise");

            migrationBuilder.DropColumn(
                name: "approval_decision_by_user_id",
                table: "enterprise");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "enterprise");

            migrationBuilder.DropColumn(
                name: "status",
                table: "enterprise");
        }
    }
}
