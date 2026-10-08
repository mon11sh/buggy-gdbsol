using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TransactionsService.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferLegsAndLimitSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "transfer_id",
                table: "transaction_logging",
                type: "integer",
                nullable: true);

            migrationBuilder.InsertData(
                table: "transfer_limits",
                columns: new[] { "privilege", "daily_limit", "per_transaction_limit" },
                values: new object[,]
                {
                    { "GOLD", 50000.00m, 25000.00m },
                    { "PREMIUM", 100000.00m, 50000.00m },
                    { "SILVER", 25000.00m, 12500.00m }
                });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_logging_transfer_id",
                table: "transaction_logging",
                column: "transfer_id");

            migrationBuilder.AddForeignKey(
                name: "fk_transaction_logging_fund_transfers_transfer_id",
                table: "transaction_logging",
                column: "transfer_id",
                principalTable: "fund_transfers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transaction_logging_fund_transfers_transfer_id",
                table: "transaction_logging");

            migrationBuilder.DropIndex(
                name: "ix_transaction_logging_transfer_id",
                table: "transaction_logging");

            migrationBuilder.DeleteData(
                table: "transfer_limits",
                keyColumn: "privilege",
                keyValue: "GOLD");

            migrationBuilder.DeleteData(
                table: "transfer_limits",
                keyColumn: "privilege",
                keyValue: "PREMIUM");

            migrationBuilder.DeleteData(
                table: "transfer_limits",
                keyColumn: "privilege",
                keyValue: "SILVER");

            migrationBuilder.DropColumn(
                name: "transfer_id",
                table: "transaction_logging");
        }
    }
}
