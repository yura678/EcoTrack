using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_subscription",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enterprise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    webhook_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    webhook_secret = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    event_types = table.Column<int[]>(type: "integer[]", nullable: true),
                    emission_source_ids = table.Column<Guid[]>(type: "uuid[]", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_subscription", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_subscription_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "usr",
                        principalTable: "users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_subscription_enterprise_id",
                table: "notification_subscription",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_subscription_user_id_enterprise_id",
                table: "notification_subscription",
                columns: new[] { "user_id", "enterprise_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_subscription");
        }
    }
}
