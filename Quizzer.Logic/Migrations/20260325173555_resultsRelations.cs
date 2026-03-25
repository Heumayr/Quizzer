using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class resultsRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuestionResult_GameGridCoordinate_GameGridCoordinateId1",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionResult_Game_GameId",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropIndex(
                name: "IX_QuestionResult_GameGridCoordinateId1",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropColumn(
                name: "GameGridCoordinateId1",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionResult_Game_GameId",
                schema: "question",
                table: "QuestionResult",
                column: "GameId",
                principalSchema: "base",
                principalTable: "Game",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuestionResult_Game_GameId",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.AddColumn<Guid>(
                name: "GameGridCoordinateId1",
                schema: "question",
                table: "QuestionResult",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionResult_GameGridCoordinateId1",
                schema: "question",
                table: "QuestionResult",
                column: "GameGridCoordinateId1");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionResult_GameGridCoordinate_GameGridCoordinateId1",
                schema: "question",
                table: "QuestionResult",
                column: "GameGridCoordinateId1",
                principalSchema: "base",
                principalTable: "GameGridCoordinate",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionResult_Game_GameId",
                schema: "question",
                table: "QuestionResult",
                column: "GameId",
                principalSchema: "base",
                principalTable: "Game",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
