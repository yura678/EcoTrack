using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceEventResolutionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "resolution_note",
                table: "compliance_event",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "resolution_reason",
                table: "compliance_event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "resolved_by_user_id",
                table: "compliance_event",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "resolution_note",
                table: "compliance_event");

            migrationBuilder.DropColumn(
                name: "resolution_reason",
                table: "compliance_event");

            migrationBuilder.DropColumn(
                name: "resolved_by_user_id",
                table: "compliance_event");
        }
    }
}
