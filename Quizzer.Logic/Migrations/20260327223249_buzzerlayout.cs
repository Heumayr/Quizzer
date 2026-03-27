using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class buzzerlayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BuzzerControlsLayout",
                schema: "question",
                table: "QuestionBase",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BuzzerMaxAllowedKeySelect",
                schema: "question",
                table: "QuestionBase",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShowTextOnKeySelect",
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
                name: "BuzzerControlsLayout",
                schema: "question",
                table: "QuestionBase");

            migrationBuilder.DropColumn(
                name: "BuzzerMaxAllowedKeySelect",
                schema: "question",
                table: "QuestionBase");

            migrationBuilder.DropColumn(
                name: "ShowTextOnKeySelect",
                schema: "question",
                table: "QuestionBase");
        }
    }
}
