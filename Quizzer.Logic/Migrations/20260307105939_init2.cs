using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class init2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerXGame_PlayerId_GamesId",
                schema: "base",
                table: "PlayerXGame");

            migrationBuilder.DropColumn(
                name: "GamesId",
                schema: "base",
                table: "PlayerXGame");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerXGame_PlayerId_GameId",
                schema: "base",
                table: "PlayerXGame",
                columns: new[] { "PlayerId", "GameId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerXGame_PlayerId_GameId",
                schema: "base",
                table: "PlayerXGame");

            migrationBuilder.AddColumn<Guid>(
                name: "GamesId",
                schema: "base",
                table: "PlayerXGame",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_PlayerXGame_PlayerId_GamesId",
                schema: "base",
                table: "PlayerXGame",
                columns: new[] { "PlayerId", "GamesId" },
                unique: true);
        }
    }
}
