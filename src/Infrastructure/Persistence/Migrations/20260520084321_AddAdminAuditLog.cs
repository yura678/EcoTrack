using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "admin-audit-log",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enterprise_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    actor_role = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    action = table.Column<int>(type: "integer", nullable: false),
                    target_type = table.Column<int>(type: "integer", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_label = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    details = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_audit_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_audit_log_enterprise_occurred_at",
                schema: "audit",
                table: "admin-audit-log",
                columns: new[] { "enterprise_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_admin_audit_log_target",
                schema: "audit",
                table: "admin-audit-log",
                columns: new[] { "target_type", "target_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin-audit-log",
                schema: "audit");
        }
    }
}
