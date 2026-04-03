using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class queststepredu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UseRandomSequenceOnNonFinishSteps",
                schema: "question",
                table: "QuestionBase",
                newName: "UseRandomSequenceOnNoneFinishSteps");

            migrationBuilder.RenameColumn(
                name: "UseProportionalPointsPerStep",
                schema: "question",
                table: "QuestionBase",
                newName: "UseProportionalScoreReductionOnStep");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UseRandomSequenceOnNoneFinishSteps",
                schema: "question",
                table: "QuestionBase",
                newName: "UseRandomSequenceOnNonFinishSteps");

            migrationBuilder.RenameColumn(
                name: "UseProportionalScoreReductionOnStep",
                schema: "question",
                table: "QuestionBase",
                newName: "UseProportionalPointsPerStep");
        }
    }
}
