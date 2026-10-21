using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class reihenfolge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SolutionNumber",
                schema: "question",
                table: "QuestionStepResource",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "OrderingQuestion",
                schema: "question",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderingQuestion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderingQuestion_QuestionBase_Id",
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
                name: "OrderingQuestion",
                schema: "question");

            migrationBuilder.DropColumn(
                name: "SolutionNumber",
                schema: "question",
                table: "QuestionStepResource");
        }
    }
}
