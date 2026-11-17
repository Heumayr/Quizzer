using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class M37_Teams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GameTeamId",
                schema: "question",
                table: "QuestionResult",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GameTeamId",
                schema: "base",
                table: "PlayerXGame",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTeamGame",
                schema: "base",
                table: "Game",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TeamCouncilSeconds",
                schema: "base",
                table: "Game",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TeamMinigameScoring",
                schema: "base",
                table: "Game",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GameTeam",
                schema: "base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ColorIndex = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    SpeakerTurn = table.Column<int>(type: "int", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTeam", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameTeam_Game_GameId",
                        column: x => x.GameId,
                        principalSchema: "base",
                        principalTable: "Game",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionResult_GameTeamId",
                schema: "question",
                table: "QuestionResult",
                column: "GameTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerXGame_GameTeamId",
                schema: "base",
                table: "PlayerXGame",
                column: "GameTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_GameTeam_GameId",
                schema: "base",
                table: "GameTeam",
                column: "GameId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerXGame_GameTeam_GameTeamId",
                schema: "base",
                table: "PlayerXGame",
                column: "GameTeamId",
                principalSchema: "base",
                principalTable: "GameTeam",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionResult_GameTeam_GameTeamId",
                schema: "question",
                table: "QuestionResult",
                column: "GameTeamId",
                principalSchema: "base",
                principalTable: "GameTeam",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerXGame_GameTeam_GameTeamId",
                schema: "base",
                table: "PlayerXGame");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionResult_GameTeam_GameTeamId",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropTable(
                name: "GameTeam",
                schema: "base");

            migrationBuilder.DropIndex(
                name: "IX_QuestionResult_GameTeamId",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropIndex(
                name: "IX_PlayerXGame_GameTeamId",
                schema: "base",
                table: "PlayerXGame");

            migrationBuilder.DropColumn(
                name: "GameTeamId",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropColumn(
                name: "GameTeamId",
                schema: "base",
                table: "PlayerXGame");

            migrationBuilder.DropColumn(
                name: "IsTeamGame",
                schema: "base",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "TeamCouncilSeconds",
                schema: "base",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "TeamMinigameScoring",
                schema: "base",
                table: "Game");
        }
    }
}
