using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class M35_KontostandUnterNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowNegativeScore",
                schema: "base",
                table: "Game",
                type: "bit",
                nullable: false,
                // Von Hand: bestehende Spiele behalten ihr Verhalten (Minus erlaubt, M35 E2). Erzeugt war false.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowNegativeScore",
                schema: "base",
                table: "Game");
        }
    }
}
