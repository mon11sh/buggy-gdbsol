using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AccountsService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_number = table.Column<int>(type: "integer", nullable: false),
                    account_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    pin_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    balance = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    privilege = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    bank_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ifsc_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    activated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    closed_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    row_version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                    table.UniqueConstraint("ak_accounts_account_number", x => x.account_number);
                });

            migrationBuilder.CreateTable(
                name: "current_account_details",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_number = table.Column<int>(type: "integer", nullable: false),
                    company_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    website = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    registration_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_current_account_details", x => x.id);
                    table.ForeignKey(
                        name: "fk_current_account_details_accounts_account_number",
                        column: x => x.account_number,
                        principalTable: "accounts",
                        principalColumn: "account_number",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "savings_account_details",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_number = table.Column<int>(type: "integer", nullable: false),
                    date_of_birth = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    phone_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    aadhar_number = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    aadhar_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_savings_account_details", x => x.id);
                    table.ForeignKey(
                        name: "fk_savings_account_details_accounts_account_number",
                        column: x => x.account_number,
                        principalTable: "accounts",
                        principalColumn: "account_number",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_account_number",
                table: "accounts",
                column: "account_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_current_account_details_account_number",
                table: "current_account_details",
                column: "account_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_current_account_details_registration_no",
                table: "current_account_details",
                column: "registration_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_savings_account_details_aadhar_hash",
                table: "savings_account_details",
                column: "aadhar_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_savings_account_details_account_number",
                table: "savings_account_details",
                column: "account_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "current_account_details");

            migrationBuilder.DropTable(
                name: "savings_account_details");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
