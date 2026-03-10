using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class questiontype : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "question");

            migrationBuilder.RenameTable(
                name: "QuestionBase",
                schema: "base",
                newName: "QuestionBase",
                newSchema: "question");

            migrationBuilder.RenameTable(
                name: "DefaultQuestion",
                schema: "questiontype",
                newName: "DefaultQuestion",
                newSchema: "question");

            migrationBuilder.AddColumn<int>(
                name: "Typ",
                schema: "question",
                table: "QuestionBase",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Typ",
                schema: "question",
                table: "QuestionBase");

            migrationBuilder.EnsureSchema(
                name: "questiontype");

            migrationBuilder.RenameTable(
                name: "QuestionBase",
                schema: "question",
                newName: "QuestionBase",
                newSchema: "base");

            migrationBuilder.RenameTable(
                name: "DefaultQuestion",
                schema: "question",
                newName: "DefaultQuestion",
                newSchema: "questiontype");
        }
    }
}
