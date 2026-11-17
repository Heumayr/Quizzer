using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>
    /// Das Tauziehen auf den Spielerkarten (M37 T9): das stärkere Team bekommt den Feldwert einmal, aufgeteilt in seiner
    /// Sprecherfolge (F-M37-06) - bei Gleichstand jedes Team vorn. Die Verlierer bekommen nichts, nie Minus (E1). Die Teams der
    /// Karten nach dem Stempel wie die Teamsumme (<see cref="MinispielTeams"/>).
    /// </summary>
    public static partial class Teamwertung
    {
        /// <summary>Je Mitglied eines Siegerteams sein Anteil als Vorschlag mit Grund; ein Anteil von 0 bringt keinen.</summary>
        public static IReadOnlyDictionary<Guid, Punktevorschlag> TauziehenVorschlaege(
            Game spiel, IEnumerable<(QuestionResult Ergebnis, string Name)> karten, TauziehenErgebnis ergebnis, int feldwert)
        {
            ArgumentNullException.ThrowIfNull(spiel);
            ArgumentNullException.ThrowIfNull(karten);
            ArgumentNullException.ThrowIfNull(ergebnis);

            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            foreach (var team in MinispielTeams(spiel, karten).Where(t => ergebnis.IstGewinner(t.Schluessel)))
            {
                foreach (var (id, anteil) in Aufteilung.Verteile(Math.Max(0, feldwert), team.Folge))
                {
                    if (anteil != 0)
                        vorschlaege[id] = new(anteil, $"{team.Name} gewinnt das Tauziehen: Anteil {Punkteanteil.Text(anteil)}");
                }
            }

            return vorschlaege;
        }
    }
}
