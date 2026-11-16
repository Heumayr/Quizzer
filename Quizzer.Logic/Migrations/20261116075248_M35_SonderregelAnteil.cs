using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class M35_SonderregelAnteil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SpecialRuleShare",
                schema: "base",
                table: "GameGridCoordinate",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SpecialRuleVariant",
                schema: "base",
                table: "GameGridCoordinate",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecialRuleShare",
                schema: "base",
                table: "GameGridCoordinate");

            migrationBuilder.DropColumn(
                name: "SpecialRuleVariant",
                schema: "base",
                table: "GameGridCoordinate");
        }
    }
}
