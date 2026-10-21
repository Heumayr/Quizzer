using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class zuordnung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PartnerText",
                schema: "question",
                table: "QuestionStepResource",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "MatchingQuestion",
                schema: "question",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchingQuestion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchingQuestion_QuestionBase_Id",
                        column: x => x.Id,
                        principalSchema: "question",
                        principalTable: "QuestionBase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchingQuestion",
                schema: "question");

            migrationBuilder.DropColumn(
                name: "PartnerText",
                schema: "question",
                table: "QuestionStepResource");
        }
    }
}
