using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class PropAppre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuestionViewKeyType",
                schema: "question",
                table: "QuestionBase",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "UseProportionalPointsPerStep",
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
                name: "QuestionViewKeyType",
                schema: "question",
                table: "QuestionBase");

            migrationBuilder.DropColumn(
                name: "UseProportionalPointsPerStep",
                schema: "question",
                table: "QuestionBase");
        }
    }
}
