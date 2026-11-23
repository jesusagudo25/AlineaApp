using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alinea.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CardsAndCommitments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TRANSACTIONS_Shape",
                table: "TRANSACTIONS");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "TRANSACTIONS",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                table: "TRANSACTIONS",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CardId",
                table: "TRANSACTIONS",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommitmentOccurrenceId",
                table: "TRANSACTIONS",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "COMMITMENTS",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    NextDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AnchorDay = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMMITMENTS", x => x.Id);
                    table.UniqueConstraint("AK_COMMITMENTS_Id_UserId", x => new { x.Id, x.UserId });
                    table.CheckConstraint("CK_COMMITMENTS_Values", "\"Kind\" IN ('income','expense') AND \"Amount\">0 AND \"Frequency\" IN ('once','weekly','fortnightly','monthly') AND \"Status\" IN ('active','paused','inactive') AND \"AnchorDay\" BETWEEN 1 AND 31");
                    table.ForeignKey(
                        name: "FK_COMMITMENTS_CATEGORIES_CategoryId_UserId",
                        columns: x => new { x.CategoryId, x.UserId },
                        principalTable: "CATEGORIES",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAYMENT_CARDS",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreditLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InitialDebt = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpeningDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosingDay = table.Column<int>(type: "integer", nullable: true),
                    PaymentDay = table.Column<int>(type: "integer", nullable: true),
                    Used = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYMENT_CARDS", x => x.Id);
                    table.UniqueConstraint("AK_PAYMENT_CARDS_Id_UserId", x => new { x.Id, x.UserId });
                    table.CheckConstraint("CK_CARDS_Shape", "(\"Kind\"='debit' AND \"AccountId\" IS NOT NULL AND \"CreditLimit\"=0 AND \"InitialDebt\"=0 AND \"ClosingDay\" IS NULL AND \"PaymentDay\" IS NULL) OR (\"Kind\"='credit' AND \"AccountId\" IS NULL AND \"CreditLimit\">0 AND \"InitialDebt\">=0 AND \"ClosingDay\" BETWEEN 1 AND 31 AND \"PaymentDay\" BETWEEN 1 AND 31 AND \"ClosingDay\" IS NOT NULL AND \"PaymentDay\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PAYMENT_CARDS_ACCOUNTS_AccountId_UserId",
                        columns: x => new { x.AccountId, x.UserId },
                        principalTable: "ACCOUNTS",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYMENT_CARDS_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COMMITMENT_OCCURRENCES",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CommitmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Concept = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMMITMENT_OCCURRENCES", x => x.Id);
                    table.UniqueConstraint("AK_COMMITMENT_OCCURRENCES_Id_UserId", x => new { x.Id, x.UserId });
                    table.ForeignKey(
                        name: "FK_COMMITMENT_OCCURRENCES_CATEGORIES_CategoryId_UserId",
                        columns: x => new { x.CategoryId, x.UserId },
                        principalTable: "CATEGORIES",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMMITMENT_OCCURRENCES_COMMITMENTS_CommitmentId_UserId",
                        columns: x => new { x.CommitmentId, x.UserId },
                        principalTable: "COMMITMENTS",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTIONS_CardId_UserId",
                table: "TRANSACTIONS",
                columns: new[] { "CardId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTIONS_CommitmentOccurrenceId",
                table: "TRANSACTIONS",
                column: "CommitmentOccurrenceId",
                unique: true,
                filter: "\"CommitmentOccurrenceId\" IS NOT NULL AND NOT \"IsVoided\"");

            migrationBuilder.CreateIndex(
                name: "IX_TRANSACTIONS_CommitmentOccurrenceId_UserId",
                table: "TRANSACTIONS",
                columns: new[] { "CommitmentOccurrenceId", "UserId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TRANSACTIONS_Shape",
                table: "TRANSACTIONS",
                sql: "(\"Kind\" IN ('income','expense') AND \"CategoryId\" IS NOT NULL AND \"DestinationAccountId\" IS NULL AND (\"AccountId\" IS NOT NULL OR (\"Kind\"='expense' AND \"CardId\" IS NOT NULL))) OR (\"Kind\"='transfer' AND \"AccountId\" IS NOT NULL AND \"CardId\" IS NULL AND \"CategoryId\" IS NULL AND \"DestinationAccountId\" IS NOT NULL AND \"AccountId\"<>\"DestinationAccountId\") OR (\"Kind\"='card_payment' AND \"CardId\" IS NOT NULL AND \"AccountId\" IS NOT NULL AND \"CategoryId\" IS NULL AND \"DestinationAccountId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_COMMITMENT_OCCURRENCES_CategoryId_UserId",
                table: "COMMITMENT_OCCURRENCES",
                columns: new[] { "CategoryId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_COMMITMENT_OCCURRENCES_CommitmentId_DueDate",
                table: "COMMITMENT_OCCURRENCES",
                columns: new[] { "CommitmentId", "DueDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COMMITMENT_OCCURRENCES_CommitmentId_UserId",
                table: "COMMITMENT_OCCURRENCES",
                columns: new[] { "CommitmentId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_COMMITMENTS_CategoryId_UserId",
                table: "COMMITMENTS",
                columns: new[] { "CategoryId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_COMMITMENTS_UserId_NextDate",
                table: "COMMITMENTS",
                columns: new[] { "UserId", "NextDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_CARDS_AccountId_UserId",
                table: "PAYMENT_CARDS",
                columns: new[] { "AccountId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_CARDS_UserId_NormalizedName",
                table: "PAYMENT_CARDS",
                columns: new[] { "UserId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TRANSACTIONS_COMMITMENT_OCCURRENCES_CommitmentOccurrenceId_~",
                table: "TRANSACTIONS",
                columns: new[] { "CommitmentOccurrenceId", "UserId" },
                principalTable: "COMMITMENT_OCCURRENCES",
                principalColumns: new[] { "Id", "UserId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TRANSACTIONS_PAYMENT_CARDS_CardId_UserId",
                table: "TRANSACTIONS",
                columns: new[] { "CardId", "UserId" },
                principalTable: "PAYMENT_CARDS",
                principalColumns: new[] { "Id", "UserId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TRANSACTIONS_COMMITMENT_OCCURRENCES_CommitmentOccurrenceId_~",
                table: "TRANSACTIONS");

            migrationBuilder.DropForeignKey(
                name: "FK_TRANSACTIONS_PAYMENT_CARDS_CardId_UserId",
                table: "TRANSACTIONS");

            migrationBuilder.DropTable(
                name: "COMMITMENT_OCCURRENCES");

            migrationBuilder.DropTable(
                name: "PAYMENT_CARDS");

            migrationBuilder.DropTable(
                name: "COMMITMENTS");

            migrationBuilder.DropIndex(
                name: "IX_TRANSACTIONS_CardId_UserId",
                table: "TRANSACTIONS");

            migrationBuilder.DropIndex(
                name: "IX_TRANSACTIONS_CommitmentOccurrenceId",
                table: "TRANSACTIONS");

            migrationBuilder.DropIndex(
                name: "IX_TRANSACTIONS_CommitmentOccurrenceId_UserId",
                table: "TRANSACTIONS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TRANSACTIONS_Shape",
                table: "TRANSACTIONS");

            migrationBuilder.DropColumn(
                name: "CardId",
                table: "TRANSACTIONS");

            migrationBuilder.DropColumn(
                name: "CommitmentOccurrenceId",
                table: "TRANSACTIONS");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "TRANSACTIONS",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(15)",
                oldMaxLength: 15);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                table: "TRANSACTIONS",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TRANSACTIONS_Shape",
                table: "TRANSACTIONS",
                sql: "(\"Kind\" IN ('income','expense') AND \"CategoryId\" IS NOT NULL AND \"DestinationAccountId\" IS NULL) OR (\"Kind\" = 'transfer' AND \"CategoryId\" IS NULL AND \"DestinationAccountId\" IS NOT NULL AND \"AccountId\" <> \"DestinationAccountId\")");
        }
    }
}
