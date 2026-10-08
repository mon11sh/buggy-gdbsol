using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionsService.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_transaction_logging_account_created",
                table: "transaction_logging",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_fund_transfers_source_created_status",
                table: "fund_transfers",
                columns: new[] { "source_account_id", "created_at", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_fund_transfers_status_created",
                table: "fund_transfers",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transaction_logging_account_created",
                table: "transaction_logging");

            migrationBuilder.DropIndex(
                name: "ix_fund_transfers_source_created_status",
                table: "fund_transfers");

            migrationBuilder.DropIndex(
                name: "ix_fund_transfers_status_created",
                table: "fund_transfers");
        }
    }
}
