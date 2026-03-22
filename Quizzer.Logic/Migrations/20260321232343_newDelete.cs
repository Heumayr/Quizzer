using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class newDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuestionResult_PlayerId",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.AddColumn<Guid>(
                name: "GameGridCoordinateId",
                schema: "question",
                table: "QuestionResult",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "GameGridCoordinateId1",
                schema: "question",
                table: "QuestionResult",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionResult_GameGridCoordinateId",
                schema: "question",
                table: "QuestionResult",
                column: "GameGridCoordinateId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionResult_GameGridCoordinateId1",
                schema: "question",
                table: "QuestionResult",
                column: "GameGridCoordinateId1");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionResult_PlayerId_QuestionBaseId_GameId_GameGridCoordinateId",
                schema: "question",
                table: "QuestionResult",
                columns: new[] { "PlayerId", "QuestionBaseId", "GameId", "GameGridCoordinateId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionResult_GameGridCoordinate_GameGridCoordinateId",
                schema: "question",
                table: "QuestionResult",
                column: "GameGridCoordinateId",
                principalSchema: "base",
                principalTable: "GameGridCoordinate",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionResult_GameGridCoordinate_GameGridCoordinateId1",
                schema: "question",
                table: "QuestionResult",
                column: "GameGridCoordinateId1",
                principalSchema: "base",
                principalTable: "GameGridCoordinate",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuestionResult_GameGridCoordinate_GameGridCoordinateId",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionResult_GameGridCoordinate_GameGridCoordinateId1",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropIndex(
                name: "IX_QuestionResult_GameGridCoordinateId",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropIndex(
                name: "IX_QuestionResult_GameGridCoordinateId1",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropIndex(
                name: "IX_QuestionResult_PlayerId_QuestionBaseId_GameId_GameGridCoordinateId",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropColumn(
                name: "GameGridCoordinateId",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.DropColumn(
                name: "GameGridCoordinateId1",
                schema: "question",
                table: "QuestionResult");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionResult_PlayerId",
                schema: "question",
                table: "QuestionResult",
                column: "PlayerId");
        }
    }
}
