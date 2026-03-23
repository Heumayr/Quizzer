using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class ResultViewNew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Correction",
                schema: "question",
                table: "QuestionResult",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CorrectionsCount",
                schema: "question",
                table: "QuestionResult",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RightCount",
                schema: "question",
                table: "QuestionResult",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WrongCount",
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
                name: "Correction",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropColumn(
                name: "CorrectionsCount",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropColumn(
                name: "RightCount",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropColumn(
                name: "WrongCount",
                schema: "question",
                table: "QuestionResult");
        }
    }
}
