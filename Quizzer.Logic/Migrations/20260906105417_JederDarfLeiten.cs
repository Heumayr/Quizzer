using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quizzer.Logic.Migrations
{
    /// <inheritdoc />
    public partial class JederDarfLeiten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nutzerentscheidung vom 2026-09-06: "jeder spieler kann sich als spielleiter
            // anmelden ... per default ... explizites wegnehmen ist sinnvoller".
            migrationBuilder.AlterColumn<bool>(
                name: "IsModerator",
                schema: "base",
                table: "Player",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            // Der Bestand wird mitgezogen, sonst wirkt die Umstellung nur fuer neue Personen -
            // und die bereits angelegten blieben von der Anmeldung ausgesperrt. Gemessen am
            // 2026-09-06: von zehn Personen standen zwei zur Wahl, beide aus den Demodaten.
            migrationBuilder.Sql("UPDATE base.Player SET IsModerator = 1 WHERE IsModerator = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Die zurueckgesetzten Rechte kommen nicht wieder: welche Person vorher keines hatte,
            // steht nirgends mehr. Nur die Voreinstellung wird gedreht.
            migrationBuilder.AlterColumn<bool>(
                name: "IsModerator",
                schema: "base",
                table: "Player",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);
        }
    }
}
