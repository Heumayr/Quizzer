using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class StepNew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFinish",
                schema: "question",
                table: "QuestionStepResource",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WarnOnFinishStep",
                schema: "question",
                table: "QuestionBase",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFinish",
                schema: "question",
                table: "QuestionStepResource");

            migrationBuilder.DropColumn(
                name: "WarnOnFinishStep",
                schema: "question",
                table: "QuestionBase");
        }
    }
}
