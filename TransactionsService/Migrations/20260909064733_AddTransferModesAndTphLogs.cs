using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TransactionsService.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferModesAndTphLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transfer_modes",
                columns: table => new
                {
                    mode = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfer_modes", x => x.mode);
                });

            migrationBuilder.CreateTable(
                name: "transfer_limit_modes",
                columns: table => new
                {
                    privilege = table.Column<string>(type: "text", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfer_limit_modes", x => new { x.privilege, x.mode });
                    table.ForeignKey(
                        name: "fk_transfer_limit_modes_transfer_limits_privilege",
                        column: x => x.privilege,
                        principalTable: "transfer_limits",
                        principalColumn: "privilege",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_transfer_limit_modes_transfer_modes_mode",
                        column: x => x.mode,
                        principalTable: "transfer_modes",
                        principalColumn: "mode",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "transfer_modes",
                columns: new[] { "mode", "description" },
                values: new object[,]
                {
                    { "IMPS", "Instant, small value" },
                    { "NEFT", "Batch settlement, any amount" },
                    { "RTGS", "Real-time gross settlement, high value" },
                    { "UPI", "Instant, small value" }
                });

            migrationBuilder.InsertData(
                table: "transfer_limit_modes",
                columns: new[] { "mode", "privilege" },
                values: new object[,]
                {
                    { "IMPS", "GOLD" },
                    { "NEFT", "GOLD" },
                    { "RTGS", "GOLD" },
                    { "UPI", "GOLD" },
                    { "IMPS", "PREMIUM" },
                    { "NEFT", "PREMIUM" },
                    { "RTGS", "PREMIUM" },
                    { "UPI", "PREMIUM" },
                    { "IMPS", "SILVER" },
                    { "NEFT", "SILVER" },
                    { "UPI", "SILVER" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_transfer_limit_modes_mode",
                table: "transfer_limit_modes",
                column: "mode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transfer_limit_modes");

            migrationBuilder.DropTable(
                name: "transfer_modes");
        }
    }
}
