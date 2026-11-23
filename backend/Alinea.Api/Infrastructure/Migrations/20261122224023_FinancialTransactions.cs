using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alinea.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UsedByTransactions",
                table: "CATEGORIES",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CATEGORIES_Id_UserId",
                table: "CATEGORIES",
                columns: new[] { "Id", "UserId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ACCOUNTS_Id_UserId",
                table: "ACCOUNTS",
                columns: new[] { "Id", "UserId" });

            migrationBuilder.CreateTable(
                name: "FINANCIAL_REQUESTS",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Response = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINANCIAL_REQUESTS", x => new { x.UserId, x.RequestId });
                    table.ForeignKey(
                        name: "FK_FINANCIAL_REQUESTS_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRANSACTIONS",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsVoided = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRANSACTIONS", x => x.Id);
                    table.UniqueConstraint("AK_TRANSACTIONS_Id_UserId", x => new { x.Id, x.UserId });
                    table.CheckConstraint("CK_TRANSACTIONS_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_TRANSACTIONS_Shape", "(\"Kind\" IN ('income','expense') AND \"CategoryId\" IS NOT NULL AND \"DestinationAccountId\" IS NULL) OR (\"Kind\" = 'transfer' AND \"CategoryId\" IS NULL AND \"DestinationAccountId\" IS NOT NULL AND \"AccountId\" <> \"DestinationAccountId\")");
                    table.ForeignKey(
                        name: "FK_TRANSACTIONS_ACCOUNTS_AccountId_UserId",
                        columns: x => new { x.AccountId, x.UserId },
                        principalTable: "ACCOUNTS",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRANSACTIONS_ACCOUNTS_DestinationAccountId_UserId",
                        columns: x => new { x.DestinationAccountId, x.UserId },
                        principalTable: "ACCOUNTS",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRANSACTIONS_CATEGORIES_CategoryId_UserId",
                        columns: x => new { x.CategoryId, x.UserId },
                        principalTable: "CATEGORIES",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRANSACTION_REVISIONS",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRANSACTION_REVISIONS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRANSACTION_REVISIONS_TRANSACTIONS_TransactionId_UserId",
                        columns: x => new { x.TransactionId, x.UserId },
                        principalTable: "TRANSACTIONS",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTION_REVISIONS_TransactionId_UserId",
                table: "TRANSACTION_REVISIONS",
                columns: new[] { "TransactionId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTIONS_AccountId_UserId",
                table: "TRANSACTIONS",
                columns: new[] { "AccountId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTIONS_CategoryId_UserId",
                table: "TRANSACTIONS",
                columns: new[] { "CategoryId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTIONS_DestinationAccountId_UserId",
                table: "TRANSACTIONS",
                columns: new[] { "DestinationAccountId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTIONS_UserId_Date_Id",
                table: "TRANSACTIONS",
                columns: new[] { "UserId", "Date", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FINANCIAL_REQUESTS");

            migrationBuilder.DropTable(
                name: "TRANSACTION_REVISIONS");

            migrationBuilder.DropTable(
                name: "TRANSACTIONS");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CATEGORIES_Id_UserId",
                table: "CATEGORIES");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ACCOUNTS_Id_UserId",
                table: "ACCOUNTS");

            migrationBuilder.DropColumn(
                name: "UsedByTransactions",
                table: "CATEGORIES");
        }
    }
}
