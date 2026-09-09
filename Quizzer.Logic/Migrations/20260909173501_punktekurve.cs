using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class punktekurve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ScoreReductionFactor",
                schema: "question",
                table: "QuestionBase",
                type: "float",
                nullable: false,
                defaultValue: 0.5);

            migrationBuilder.AddColumn<int>(
                name: "ScoreReductionMode",
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
                name: "ScoreReductionFactor",
                schema: "question",
                table: "QuestionBase");

            migrationBuilder.DropColumn(
                name: "ScoreReductionMode",
                schema: "question",
                table: "QuestionBase");
        }
    }
}
