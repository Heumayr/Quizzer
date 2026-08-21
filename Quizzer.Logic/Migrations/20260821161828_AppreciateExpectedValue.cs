using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class AppreciateExpectedValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedDate",
                schema: "question",
                table: "AppreciateQestion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ExpectedValue",
                schema: "question",
                table: "AppreciateQestion",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Unit",
                schema: "question",
                table: "AppreciateQestion",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ValueKind",
                schema: "question",
                table: "AppreciateQestion",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedDate",
                schema: "question",
                table: "AppreciateQestion");

            migrationBuilder.DropColumn(
                name: "ExpectedValue",
                schema: "question",
                table: "AppreciateQestion");

            migrationBuilder.DropColumn(
                name: "Unit",
                schema: "question",
                table: "AppreciateQestion");

            migrationBuilder.DropColumn(
                name: "ValueKind",
                schema: "question",
                table: "AppreciateQestion");
        }
    }
}
