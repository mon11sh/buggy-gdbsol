using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountsService.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_accounts_privilege",
                table: "accounts",
                column: "privilege");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_type_active",
                table: "accounts",
                columns: new[] { "account_type", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_accounts_privilege",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_accounts_type_active",
                table: "accounts");
        }
    }
}
