using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class Moderator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ModeratorPlayerId",
                schema: "base",
                table: "Game",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Game_ModeratorPlayerId",
                schema: "base",
                table: "Game",
                column: "ModeratorPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Game_Player_ModeratorPlayerId",
                schema: "base",
                table: "Game",
                column: "ModeratorPlayerId",
                principalSchema: "base",
                principalTable: "Player",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Game_Player_ModeratorPlayerId",
                schema: "base",
                table: "Game");

            migrationBuilder.DropIndex(
                name: "IX_Game_ModeratorPlayerId",
                schema: "base",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "ModeratorPlayerId",
                schema: "base",
                table: "Game");
        }
    }
}
