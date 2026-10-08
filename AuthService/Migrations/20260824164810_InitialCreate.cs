using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auth_audit_logs",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(36)", nullable: false),
                    login_id = table.Column<string>(type: "varchar(255)", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<string>(type: "varchar(30)", nullable: false),
                    reason = table.Column<string>(type: "varchar(500)", nullable: true),
                    ip_address = table.Column<string>(type: "varchar(45)", nullable: true),
                    user_agent = table.Column<string>(type: "varchar(1000)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auth_tokens",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(36)", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    login_id = table.Column<string>(type: "varchar(255)", nullable: false),
                    token_jti = table.Column<string>(type: "varchar(255)", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auth_audit_logs_action",
                table: "auth_audit_logs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "ix_auth_audit_logs_created_at",
                table: "auth_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_auth_audit_logs_login_id",
                table: "auth_audit_logs",
                column: "login_id");

            migrationBuilder.CreateIndex(
                name: "ix_auth_audit_logs_user_id",
                table: "auth_audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_auth_tokens_expires_at",
                table: "auth_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_auth_tokens_is_revoked",
                table: "auth_tokens",
                column: "is_revoked");

            migrationBuilder.CreateIndex(
                name: "ix_auth_tokens_token_jti",
                table: "auth_tokens",
                column: "token_jti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auth_tokens_user_id",
                table: "auth_tokens",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_audit_logs");

            migrationBuilder.DropTable(
                name: "auth_tokens");
        }
    }
}
