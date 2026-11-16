using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class M35_Bestenliste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecordValue",
                schema: "question",
                table: "QuestionResult",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecordedAt",
                schema: "question",
                table: "QuestionResult",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Steals",
                schema: "question",
                table: "QuestionResult",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordValue",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropColumn(
                name: "RecordedAt",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropColumn(
                name: "Steals",
                schema: "question",
                table: "QuestionResult");
        }
    }
}
