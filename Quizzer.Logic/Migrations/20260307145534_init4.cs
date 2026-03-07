using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class init4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Header_Game_GameId",
                schema: "base",
                table: "Header");

            migrationBuilder.DropForeignKey(
                name: "FK_Header_Game_GameId1",
                schema: "base",
                table: "Header");

            migrationBuilder.DropIndex(
                name: "IX_Header_GameId1",
                schema: "base",
                table: "Header");

            migrationBuilder.DropColumn(
                name: "GameColumnId",
                schema: "base",
                table: "Header");

            migrationBuilder.DropColumn(
                name: "GameId1",
                schema: "base",
                table: "Header");

            migrationBuilder.DropColumn(
                name: "GameRowId",
                schema: "base",
                table: "Header");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                schema: "base",
                table: "Header",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeaderType",
                schema: "base",
                table: "Header",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_Header_Game_GameId",
                schema: "base",
                table: "Header",
                column: "GameId",
                principalSchema: "base",
                principalTable: "Game",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Header_Game_GameId",
                schema: "base",
                table: "Header");

            migrationBuilder.DropColumn(
                name: "HeaderType",
                schema: "base",
                table: "Header");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                schema: "base",
                table: "Header",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "GameColumnId",
                schema: "base",
                table: "Header",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "GameId1",
                schema: "base",
                table: "Header",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GameRowId",
                schema: "base",
                table: "Header",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Header_GameId1",
                schema: "base",
                table: "Header",
                column: "GameId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Header_Game_GameId",
                schema: "base",
                table: "Header",
                column: "GameId",
                principalSchema: "base",
                principalTable: "Game",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Header_Game_GameId1",
                schema: "base",
                table: "Header",
                column: "GameId1",
                principalSchema: "base",
                principalTable: "Game",
                principalColumn: "Id");
        }
    }
}
