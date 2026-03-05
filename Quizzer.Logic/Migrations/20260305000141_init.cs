using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "base");

            migrationBuilder.EnsureSchema(
                name: "questiontype");

            migrationBuilder.CreateTable(
                name: "Category",
                schema: "base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Category", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Game",
                schema: "base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Restart = table.Column<bool>(type: "bit", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Depth = table.Column<int>(type: "int", nullable: false),
                    CellHeight = table.Column<double>(type: "float", nullable: false),
                    CellWidth = table.Column<double>(type: "float", nullable: false),
                    DifficultyMultiplier = table.Column<double>(type: "float", nullable: false),
                    DifficultyAddition = table.Column<int>(type: "int", nullable: false),
                    DifficultyMinusMultiplier = table.Column<double>(type: "float", nullable: false),
                    DifficultyMinusAddition = table.Column<int>(type: "int", nullable: false),
                    PhaseMultiplier = table.Column<double>(type: "float", nullable: false),
                    PhaseAddition = table.Column<int>(type: "int", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Game", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Player",
                schema: "base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Player", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuestionBase",
                schema: "base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DesignationShort = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    MinusPoints = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Difficulty = table.Column<int>(type: "int", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionBase", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionBase_Category_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "base",
                        principalTable: "Category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Header",
                schema: "base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameColumnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GameId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Header", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Header_Game_GameId",
                        column: x => x.GameId,
                        principalSchema: "base",
                        principalTable: "Game",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Header_Game_GameId1",
                        column: x => x.GameId1,
                        principalSchema: "base",
                        principalTable: "Game",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerXGame",
                schema: "base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GamesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerXGame", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerXGame_Game_GameId",
                        column: x => x.GameId,
                        principalSchema: "base",
                        principalTable: "Game",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerXGame_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "base",
                        principalTable: "Player",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DefaultQuestion",
                schema: "questiontype",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefaultQuestion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DefaultQuestion_QuestionBase_Id",
                        column: x => x.Id,
                        principalSchema: "base",
                        principalTable: "QuestionBase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameGridCoordinate",
                schema: "base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    X = table.Column<int>(type: "int", nullable: false),
                    Y = table.Column<int>(type: "int", nullable: false),
                    Z = table.Column<int>(type: "int", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    QuestionBaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDone = table.Column<bool>(type: "bit", nullable: false),
                    CurrentPoints = table.Column<int>(type: "int", nullable: false),
                    CurrentMinusPoints = table.Column<int>(type: "int", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameGridCoordinate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameGridCoordinate_Game_GameId",
                        column: x => x.GameId,
                        principalSchema: "base",
                        principalTable: "Game",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameGridCoordinate_QuestionBase_QuestionBaseId",
                        column: x => x.QuestionBaseId,
                        principalSchema: "base",
                        principalTable: "QuestionBase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionResult",
                schema: "base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionBaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectAnswered = table.Column<bool>(type: "bit", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    MinusScore = table.Column<int>(type: "int", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionResult", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionResult_Game_GameId",
                        column: x => x.GameId,
                        principalSchema: "base",
                        principalTable: "Game",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuestionResult_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "base",
                        principalTable: "Player",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuestionResult_QuestionBase_QuestionBaseId",
                        column: x => x.QuestionBaseId,
                        principalSchema: "base",
                        principalTable: "QuestionBase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionStepResource",
                schema: "base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionBaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsResult = table.Column<bool>(type: "bit", nullable: false),
                    SquenceNumber = table.Column<int>(type: "int", nullable: false),
                    StepText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResourceFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResourceTyp = table.Column<int>(type: "int", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionStepResource", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionStepResource_QuestionBase_QuestionBaseId",
                        column: x => x.QuestionBaseId,
                        principalSchema: "base",
                        principalTable: "QuestionBase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameGridCoordinate_GameId",
                schema: "base",
                table: "GameGridCoordinate",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameGridCoordinate_QuestionBaseId",
                schema: "base",
                table: "GameGridCoordinate",
                column: "QuestionBaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Header_GameId",
                schema: "base",
                table: "Header",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Header_GameId1",
                schema: "base",
                table: "Header",
                column: "GameId1");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerXGame_GameId",
                schema: "base",
                table: "PlayerXGame",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerXGame_PlayerId_GamesId",
                schema: "base",
                table: "PlayerXGame",
                columns: new[] { "PlayerId", "GamesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBase_CategoryId",
                schema: "base",
                table: "QuestionBase",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionResult_GameId",
                schema: "base",
                table: "QuestionResult",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionResult_PlayerId",
                schema: "base",
                table: "QuestionResult",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionResult_QuestionBaseId",
                schema: "base",
                table: "QuestionResult",
                column: "QuestionBaseId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionStepResource_QuestionBaseId",
                schema: "base",
                table: "QuestionStepResource",
                column: "QuestionBaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DefaultQuestion",
                schema: "questiontype");

            migrationBuilder.DropTable(
                name: "GameGridCoordinate",
                schema: "base");

            migrationBuilder.DropTable(
                name: "Header",
                schema: "base");

            migrationBuilder.DropTable(
                name: "PlayerXGame",
                schema: "base");

            migrationBuilder.DropTable(
                name: "QuestionResult",
                schema: "base");

            migrationBuilder.DropTable(
                name: "QuestionStepResource",
                schema: "base");

            migrationBuilder.DropTable(
                name: "Game",
                schema: "base");

            migrationBuilder.DropTable(
                name: "Player",
                schema: "base");

            migrationBuilder.DropTable(
                name: "QuestionBase",
                schema: "base");

            migrationBuilder.DropTable(
                name: "Category",
                schema: "base");
        }
    }
}
