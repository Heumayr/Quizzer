using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class sxs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "QuestionStepResource",
                schema: "base",
                newName: "QuestionStepResource",
                newSchema: "question");

            migrationBuilder.RenameTable(
                name: "QuestionResult",
                schema: "base",
                newName: "QuestionResult",
                newSchema: "question");

            migrationBuilder.AddColumn<string>(
                name: "GroupKey",
                schema: "question",
                table: "QuestionStepResource",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ResultType",
                schema: "question",
                table: "QuestionStepResource",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StepXStep",
                schema: "question",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepXStep", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StepXStep_QuestionStepResource_FromId",
                        column: x => x.FromId,
                        principalSchema: "question",
                        principalTable: "QuestionStepResource",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StepXStep_QuestionStepResource_ToId",
                        column: x => x.ToId,
                        principalSchema: "question",
                        principalTable: "QuestionStepResource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StepXStep_FromId_ToId",
                schema: "question",
                table: "StepXStep",
                columns: new[] { "FromId", "ToId" },
                unique: true,
                filter: "[FromId] IS NOT NULL AND [ToId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StepXStep_ToId",
                schema: "question",
                table: "StepXStep",
                column: "ToId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StepXStep",
                schema: "question");

            migrationBuilder.DropColumn(
                name: "GroupKey",
                schema: "question",
                table: "QuestionStepResource");

            migrationBuilder.DropColumn(
                name: "ResultType",
                schema: "question",
                table: "QuestionStepResource");

            migrationBuilder.RenameTable(
                name: "QuestionStepResource",
                schema: "question",
                newName: "QuestionStepResource",
                newSchema: "base");

            migrationBuilder.RenameTable(
                name: "QuestionResult",
                schema: "question",
                newName: "QuestionResult",
                newSchema: "base");
        }
    }
}
