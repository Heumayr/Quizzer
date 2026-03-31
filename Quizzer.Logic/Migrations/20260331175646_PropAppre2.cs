using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class PropAppre2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppreciateQestion",
                schema: "question",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppreciateQestion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppreciateQestion_QuestionBase_Id",
                        column: x => x.Id,
                        principalSchema: "question",
                        principalTable: "QuestionBase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertiesQuestion",
                schema: "question",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertiesQuestion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertiesQuestion_QuestionBase_Id",
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
                name: "AppreciateQestion",
                schema: "question");

            migrationBuilder.DropTable(
                name: "PropertiesQuestion",
                schema: "question");
        }
    }
}
