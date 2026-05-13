using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_roles_enterprise_enterprise_id",
                schema: "usr",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "fk_users_enterprise_enterprise_id",
                schema: "usr",
                table: "users");

            migrationBuilder.CreateTable(
                name: "user_enterprise_membership",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enterprise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_enterprise_membership", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_enterprise_membership_enterprise_enterprise_id",
                        column: x => x.enterprise_id,
                        principalTable: "enterprise",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_enterprise_membership_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "usr",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_enterprise_membership_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "usr",
                        principalTable: "users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_enterprise_membership_enterprise_id",
                table: "user_enterprise_membership",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_enterprise_membership_role_id",
                table: "user_enterprise_membership",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_enterprise_membership_user_id_enterprise_id",
                table: "user_enterprise_membership",
                columns: new[] { "user_id", "enterprise_id" },
                unique: true);

            // Backfill memberships from existing users.enterprise_id + their first user_role
            // For each user with an EnterpriseId and at least one role, create one active membership.
            migrationBuilder.Sql(@"
                INSERT INTO user_enterprise_membership (id, user_id, enterprise_id, role_id, joined_at)
                SELECT gen_random_uuid(), u.""UserId"", u.enterprise_id, ur.role_id, timezone('utc', now())
                FROM usr.users u
                JOIN usr.user_role ur ON ur.user_id = u.""UserId""
                WHERE u.enterprise_id IS NOT NULL
                  AND u.enterprise_id <> '00000000-0000-0000-0000-000000000000'
                ON CONFLICT DO NOTHING;");

            migrationBuilder.DropIndex(
                name: "ix_users_enterprise_id",
                schema: "usr",
                table: "users");

            migrationBuilder.DropColumn(
                name: "enterprise_id",
                schema: "usr",
                table: "users");

            migrationBuilder.AlterColumn<Guid>(
                name: "enterprise_id",
                schema: "usr",
                table: "roles",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "fk_roles_enterprise_enterprise_id",
                schema: "usr",
                table: "roles",
                column: "enterprise_id",
                principalTable: "enterprise",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_roles_enterprise_enterprise_id",
                schema: "usr",
                table: "roles");

            migrationBuilder.DropTable(
                name: "user_enterprise_membership");

            migrationBuilder.AddColumn<Guid>(
                name: "enterprise_id",
                schema: "usr",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "enterprise_id",
                schema: "usr",
                table: "roles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_enterprise_id",
                schema: "usr",
                table: "users",
                column: "enterprise_id");

            migrationBuilder.AddForeignKey(
                name: "fk_roles_enterprise_enterprise_id",
                schema: "usr",
                table: "roles",
                column: "enterprise_id",
                principalTable: "enterprise",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_users_enterprise_enterprise_id",
                schema: "usr",
                table: "users",
                column: "enterprise_id",
                principalTable: "enterprise",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
