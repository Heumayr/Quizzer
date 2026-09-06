using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class GameTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GameThemeId",
                schema: "base",
                table: "Game",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameTheme",
                schema: "base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FolderName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BackgroundColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ForegroundColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CellTextColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HeaderTextColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTheme", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Game_GameThemeId",
                schema: "base",
                table: "Game",
                column: "GameThemeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Game_GameTheme_GameThemeId",
                schema: "base",
                table: "Game",
                column: "GameThemeId",
                principalSchema: "base",
                principalTable: "GameTheme",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Game_GameTheme_GameThemeId",
                schema: "base",
                table: "Game");

            migrationBuilder.DropTable(
                name: "GameTheme",
                schema: "base");

            migrationBuilder.DropIndex(
                name: "IX_Game_GameThemeId",
                schema: "base",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "GameThemeId",
                schema: "base",
                table: "Game");
        }
    }
}
