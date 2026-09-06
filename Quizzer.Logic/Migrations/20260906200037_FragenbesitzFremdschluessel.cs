using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class FragenbesitzFremdschluessel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_QuestionBase_OwnerPlayerId",
                schema: "question",
                table: "QuestionBase",
                column: "OwnerPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionBase_Player_OwnerPlayerId",
                schema: "question",
                table: "QuestionBase",
                column: "OwnerPlayerId",
                principalSchema: "base",
                principalTable: "Player",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuestionBase_Player_OwnerPlayerId",
                schema: "question",
                table: "QuestionBase");

            migrationBuilder.DropIndex(
                name: "IX_QuestionBase_OwnerPlayerId",
                schema: "question",
                table: "QuestionBase");
        }
    }
}
