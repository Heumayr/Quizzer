using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class M35_Sonderregeln : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SpecialRule",
                schema: "base",
                table: "GameGridCoordinate",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SpecialRuleAnnounced",
                schema: "base",
                table: "GameGridCoordinate",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecialRule",
                schema: "base",
                table: "GameGridCoordinate");

            migrationBuilder.DropColumn(
                name: "SpecialRuleAnnounced",
                schema: "base",
                table: "GameGridCoordinate");
        }
    }
}
