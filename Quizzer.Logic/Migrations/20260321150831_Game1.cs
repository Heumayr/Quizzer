using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class Game1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameGridCoordinate_QuestionBase_QuestionBaseId",
                schema: "base",
                table: "GameGridCoordinate");

            migrationBuilder.AlterColumn<Guid>(
                name: "QuestionBaseId",
                schema: "base",
                table: "GameGridCoordinate",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_GameGridCoordinate_QuestionBase_QuestionBaseId",
                schema: "base",
                table: "GameGridCoordinate",
                column: "QuestionBaseId",
                principalSchema: "question",
                principalTable: "QuestionBase",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameGridCoordinate_QuestionBase_QuestionBaseId",
                schema: "base",
                table: "GameGridCoordinate");

            migrationBuilder.AlterColumn<Guid>(
                name: "QuestionBaseId",
                schema: "base",
                table: "GameGridCoordinate",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GameGridCoordinate_QuestionBase_QuestionBaseId",
                schema: "base",
                table: "GameGridCoordinate",
                column: "QuestionBaseId",
                principalSchema: "question",
                principalTable: "QuestionBase",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
