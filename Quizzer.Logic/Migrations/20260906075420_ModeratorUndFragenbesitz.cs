using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class ModeratorUndFragenbesitz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerPlayerId",
                schema: "question",
                table: "QuestionBase",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsModerator",
                schema: "base",
                table: "Player",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                schema: "base",
                table: "Player",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerPlayerId",
                schema: "question",
                table: "QuestionBase");

            migrationBuilder.DropColumn(
                name: "IsModerator",
                schema: "base",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                schema: "base",
                table: "Player");
        }
    }
}
