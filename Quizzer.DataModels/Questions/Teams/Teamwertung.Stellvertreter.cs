using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>
    /// Das Stellvertreter-Duell auf den Spielerkarten (M37 T10): das Team des Siegers bekommt den Feldwert einmal, aufgeteilt in
    /// seiner Sprecherfolge (F-M37-06) - bei gleicher Bestzeit jedes Team vorn. Die anderen bekommen nichts, nie Minus (E1).
    /// Welches Team gewinnt, sagt der Stempel der Karte des Stellvertreters, wie bei der Teamsumme.
    /// </summary>
    public static partial class Teamwertung
    {
        /// <summary>Je Mitglied eines Siegerteams sein Anteil als Vorschlag mit Grund; ein Anteil von 0 bringt keinen.</summary>
        public static IReadOnlyDictionary<Guid, Punktevorschlag> StellvertreterVorschlaege(
            Game spiel, IEnumerable<(QuestionResult Ergebnis, string Name)> karten, StellvertreterErgebnis ergebnis, int feldwert)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            return SiegerteamVorschlaege(spiel, karten, team => team.Folge.Any(ergebnis.Sieger.Contains), feldwert, "das Stellvertreter-Duell");
        }
    }
}
