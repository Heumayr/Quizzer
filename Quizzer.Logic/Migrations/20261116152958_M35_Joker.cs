using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class M35_Joker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerJoker",
                schema: "base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GameGridCoordinateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TargetPlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerJoker", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerJoker_GameGridCoordinate_GameGridCoordinateId",
                        column: x => x.GameGridCoordinateId,
                        principalSchema: "base",
                        principalTable: "GameGridCoordinate",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerJoker_Game_GameId",
                        column: x => x.GameId,
                        principalSchema: "base",
                        principalTable: "Game",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerJoker_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "base",
                        principalTable: "Player",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerJoker_Player_TargetPlayerId",
                        column: x => x.TargetPlayerId,
                        principalSchema: "base",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerJoker_GameGridCoordinateId",
                schema: "base",
                table: "PlayerJoker",
                column: "GameGridCoordinateId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerJoker_GameId",
                schema: "base",
                table: "PlayerJoker",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerJoker_PlayerId",
                schema: "base",
                table: "PlayerJoker",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerJoker_TargetPlayerId",
                schema: "base",
                table: "PlayerJoker",
                column: "TargetPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerJoker",
                schema: "base");
        }
    }
}
