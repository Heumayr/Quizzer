using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class M36_ZweiFaktor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TotpEnabled",
                schema: "auth",
                table: "Account",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "TotpLastStep",
                schema: "auth",
                table: "Account",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TotpSecretProtected",
                schema: "auth",
                table: "Account",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountRecoveryCode",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRecoveryCode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountRecoveryCode_Account_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "auth",
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryCode_AccountId_CodeHash",
                schema: "auth",
                table: "AccountRecoveryCode",
                columns: new[] { "AccountId", "CodeHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountRecoveryCode",
                schema: "auth");

            migrationBuilder.DropColumn(
                name: "TotpEnabled",
                schema: "auth",
                table: "Account");

            migrationBuilder.DropColumn(
                name: "TotpLastStep",
                schema: "auth",
                table: "Account");

            migrationBuilder.DropColumn(
                name: "TotpSecretProtected",
                schema: "auth",
                table: "Account");
        }
    }
}
