using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>
    /// Die Staffel auf den Spielerkarten (M37 T11): das Team mit den meisten Teilen bekommt den Feldwert einmal, aufgeteilt in seiner
    /// Sprecherfolge (F-M37-06) - bei Gleichstand jedes Team vorn. Die anderen bekommen nichts, nie Minus (E1). Die Teams der Karten
    /// nach dem Stempel wie die Teamsumme; gemeinsamer Weg mit Tauziehen und Stellvertreter-Duell.
    /// </summary>
    public static partial class Teamwertung
    {
        /// <summary>Je Mitglied eines Siegerteams sein Anteil als Vorschlag mit Grund; ein Anteil von 0 bringt keinen.</summary>
        public static IReadOnlyDictionary<Guid, Punktevorschlag> StaffelVorschlaege(
            Game spiel, IEnumerable<(QuestionResult Ergebnis, string Name)> karten, StaffelErgebnis ergebnis, int feldwert)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            return SiegerteamVorschlaege(spiel, karten, team => ergebnis.IstGewinner(team.Schluessel), feldwert, "die Staffel");
        }
    }
}
