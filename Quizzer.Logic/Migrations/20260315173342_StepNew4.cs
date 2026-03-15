using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class StepNew4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ResultType",
                schema: "question",
                table: "QuestionStepResource",
                newName: "FinishType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FinishType",
                schema: "question",
                table: "QuestionStepResource",
                newName: "ResultType");
        }
    }
}
