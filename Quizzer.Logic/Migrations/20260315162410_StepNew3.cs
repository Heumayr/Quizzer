using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class StepNew3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultFinishType",
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
                name: "DefaultFinishType",
                schema: "question",
                table: "QuestionBase");
        }
    }
}
