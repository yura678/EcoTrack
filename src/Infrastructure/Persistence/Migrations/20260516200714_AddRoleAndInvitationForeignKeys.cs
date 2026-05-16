using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleAndInvitationForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_enterprise_invitation_enterprise_enterprise_id",
                table: "enterprise_invitation");

            migrationBuilder.DropForeignKey(
                name: "fk_roles_enterprise_enterprise_id",
                schema: "usr",
                table: "roles");

            migrationBuilder.DropPrimaryKey(
                name: "pk_enterprise_invitation",
                table: "enterprise_invitation");

            migrationBuilder.DropIndex(
                name: "ix_enterprise_invitation_enterprise_id",
                table: "enterprise_invitation");

            migrationBuilder.RenameTable(
                name: "enterprise_invitation",
                newName: "enterprise-invitations",
                newSchema: "usr");

            migrationBuilder.AlterColumn<string>(
                name: "token",
                schema: "usr",
                table: "enterprise-invitations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "usr",
                table: "enterprise-invitations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "pk_enterprise_invitations",
                schema: "usr",
                table: "enterprise-invitations",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_user_refresh_tokens_enterprise_id",
                schema: "usr",
                table: "user-refresh-tokens",
                column: "enterprise_id");

            migrationBuilder.CreateIndex(
                name: "ix_enterprise_invitations_enterprise_id_email",
                schema: "usr",
                table: "enterprise-invitations",
                columns: new[] { "enterprise_id", "email" });

            migrationBuilder.CreateIndex(
                name: "ix_enterprise_invitations_role_id",
                schema: "usr",
                table: "enterprise-invitations",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_enterprise_invitations_token",
                schema: "usr",
                table: "enterprise-invitations",
                column: "token",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_enterprise_invitations_asp_net_roles_role_id",
                schema: "usr",
                table: "enterprise-invitations",
                column: "role_id",
                principalSchema: "usr",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_enterprise_invitations_enterprise_enterprise_id",
                schema: "usr",
                table: "enterprise-invitations",
                column: "enterprise_id",
                principalTable: "enterprise",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_roles_enterprise_enterprise_id",
                schema: "usr",
                table: "roles",
                column: "enterprise_id",
                principalTable: "enterprise",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_refresh_tokens_enterprise_enterprise_id",
                schema: "usr",
                table: "user-refresh-tokens",
                column: "enterprise_id",
                principalTable: "enterprise",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_enterprise_invitations_asp_net_roles_role_id",
                schema: "usr",
                table: "enterprise-invitations");

            migrationBuilder.DropForeignKey(
                name: "fk_enterprise_invitations_enterprise_enterprise_id",
                schema: "usr",
                table: "enterprise-invitations");

            migrationBuilder.DropForeignKey(
                name: "fk_roles_enterprise_enterprise_id",
                schema: "usr",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "fk_user_refresh_tokens_enterprise_enterprise_id",
                schema: "usr",
                table: "user-refresh-tokens");

            migrationBuilder.DropIndex(
                name: "ix_user_refresh_tokens_enterprise_id",
                schema: "usr",
                table: "user-refresh-tokens");

            migrationBuilder.DropPrimaryKey(
                name: "pk_enterprise_invitations",
                schema: "usr",
                table: "enterprise-invitations");

            migrationBuilder.DropIndex(
                name: "ix_enterprise_invitations_enterprise_id_email",
                schema: "usr",
                table: "enterprise-invitations");

            migrationBuilder.DropIndex(
                name: "ix_enterprise_invitations_role_id",
                schema: "usr",
                table: "enterprise-invitations");

            migrationBuilder.DropIndex(
                name: "ix_enterprise_invitations_token",
                schema: "usr",
                table: "enterprise-invitations");

            migrationBuilder.RenameTable(
                name: "enterprise-invitations",
                schema: "usr",
                newName: "enterprise_invitation");

            migrationBuilder.AlterColumn<string>(
                name: "token",
                table: "enterprise_invitation",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "enterprise_invitation",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddPrimaryKey(
                name: "pk_enterprise_invitation",
                table: "enterprise_invitation",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_enterprise_invitation_enterprise_id",
                table: "enterprise_invitation",
                column: "enterprise_id");

            migrationBuilder.AddForeignKey(
                name: "fk_enterprise_invitation_enterprise_enterprise_id",
                table: "enterprise_invitation",
                column: "enterprise_id",
                principalTable: "enterprise",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_roles_enterprise_enterprise_id",
                schema: "usr",
                table: "roles",
                column: "enterprise_id",
                principalTable: "enterprise",
                principalColumn: "id");
        }
    }
}
