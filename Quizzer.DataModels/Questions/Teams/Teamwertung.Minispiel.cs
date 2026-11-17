using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>Ein Team im zusammengefassten Minispiel (M37 T8): Schlüssel (ohne Team der Spieler selbst), Name, Mitglieder in Sprecherfolge.</summary>
    public sealed record MinispielTeam(Guid Schluessel, string Name, IReadOnlyList<Guid> Folge);

    /// <summary>Was ein Team aus dem Minispiel bekommt: Wert, ob der Durchschnitt am Feldwert gedeckelt wurde, je Mitglied der Anteil.</summary>
    public sealed record MinispielTeamwert(
        MinispielTeam Team, TeamMinigameScoring Wertung, int Wert, bool Gedeckelt, IReadOnlyDictionary<Guid, int> Anteile);

    /// <summary>
    /// Minispiele je Team (M37 T8, Entwurf Abschnitte 3 und 7): keine neue Art, eine Schicht über den Karten. Die Art wertet
    /// je Spieler wie immer; eine Art mit <see cref="MinispielTeamwertung.Zusammengefasst"/> fasst die Einzelwerte eines Teams
    /// als Summe oder Durchschnitt (F-M37-11, Einstellung je Spiel) und teilt den Teamwert auf (F-M37-06).
    /// </summary>
    public static partial class Teamwertung
    {
        /// <summary>Wie ein Spieler ohne Team im Grund heißt - er ist sein eigenes Team (fail-closed).</summary>
        public const string OhneTeamName = "ohne Team";

        /// <summary>Ob die Zelle ein Minispiel trägt, das in diesem Spiel je Team zusammengefasst wird - Karte und Ergebnisfenster fragen hier.</summary>
        public static bool FasstMinispielZusammen(Game? spiel, QuestionBase? frage)
            => spiel is { IsTeamGame: true }
               && frage is MinigameQuestion minispiel
               && MinigameProfiles.TryFor(minispiel.Kind, out var art)
               && art.TeamScoring == MinispielTeamwertung.Zusammengefasst;

        /// <summary>
        /// Die Teams der Karten einer Zelle - nach dem Stempel wie die Teamsumme (<see cref="TeamDes"/>), je Team die Sprecherfolge
        /// ab dessen Stand. Wer ohne Team ist, bildet sein eigenes.
        /// </summary>
        public static IReadOnlyList<MinispielTeam> MinispielTeams(Game spiel, IEnumerable<(QuestionResult Ergebnis, string Name)> karten)
        {
            ArgumentNullException.ThrowIfNull(spiel);
            ArgumentNullException.ThrowIfNull(karten);

            return karten
                .GroupBy(k => TeamDes(spiel, k.Ergebnis) ?? k.Ergebnis.PlayerId)
                .Select(g =>
                {
                    var team = spiel.GameTeams.FirstOrDefault(t => t.Id == g.Key);
                    var mitglieder = g.Select(k => new Teammitglied(k.Ergebnis.PlayerId, k.Name));
                    return new MinispielTeam(g.Key, team?.Name ?? OhneTeamName, Sprecherfolge.Folge(mitglieder, team?.SpeakerTurn ?? 0));
                })
                .ToList();
        }

        /// <summary>
        /// Summe oder Durchschnitt der Einzelwerte, danach <see cref="Aufteilung"/>. Wer keinen Wert hat, zählt mit 0 - sonst
        /// wöge ein einzelner Sieger im Durchschnitt wie das ganze Team. Beim Durchschnitt ist der Feldwert die Obergrenze; eine
        /// unbekannte Wertung bewegt nichts (fail-closed).
        /// </summary>
        public static MinispielTeamwert FasseMinispiel(
            MinispielTeam team, IReadOnlyDictionary<Guid, int> einzeln, TeamMinigameScoring wertung, int feldwert)
        {
            ArgumentNullException.ThrowIfNull(team);
            ArgumentNullException.ThrowIfNull(einzeln);

            var folge = team.Folge.Distinct().ToList();
            var summe = folge.Sum(id => einzeln.GetValueOrDefault(id));

            var (wert, gedeckelt) = wertung switch
            {
                TeamMinigameScoring.Sum => (summe, false),
                TeamMinigameScoring.Average when folge.Count > 0 => Durchschnitt(summe, folge.Count, feldwert),
                _ => (0, false),
            };

            return new(team, wertung, wert, gedeckelt, Aufteilung.Verteile(wert, folge));
        }

        private static (int Wert, bool Gedeckelt) Durchschnitt(int summe, int mitglieder, int feldwert)
        {
            var schnitt = (int)Math.Round(summe / (double)mitglieder, MidpointRounding.AwayFromZero);
            var grenze = Math.Max(0, feldwert);

            return schnitt > grenze ? (grenze, true) : (schnitt, false);
        }

        /// <summary>
        /// Je Mitglied sein Anteil als Vorschlag mit Grund (E1), ein Minus am eigenen Kontostand gekappt (E2) - der gekappte Rest
        /// wird nicht umgelegt. Ein Anteil von 0 bringt keinen Vorschlag.
        /// </summary>
        public static IReadOnlyDictionary<Guid, Punktevorschlag> MinispielVorschlaege(MinispielTeamwert wert, Kontostaende konten)
        {
            ArgumentNullException.ThrowIfNull(wert);
            ArgumentNullException.ThrowIfNull(konten);

            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            foreach (var (id, anteil) in wert.Anteile)
            {
                var gekappt = konten.Kappe(id, anteil);

                if (gekappt != 0)
                    vorschlaege[id] = new(gekappt, $"{Teamsatz(wert)}: Anteil {Punkteanteil.Text(gekappt)}"
                                                   + (gekappt != anteil ? Punkteanteil.GekapptText : string.Empty));
            }

            return vorschlaege;
        }

        /// <summary>„Rot – Durchschnitt +100“ bzw. „Rot – Summe +300“; gedeckelt mit Hinweis.</summary>
        public static string Teamsatz(MinispielTeamwert wert)
        {
            ArgumentNullException.ThrowIfNull(wert);

            var art = wert.Wertung == TeamMinigameScoring.Sum ? "Summe" : "Durchschnitt";
            return $"{wert.Team.Name} – {art} {Punkteanteil.Text(wert.Wert)}" + (wert.Gedeckelt ? " (höchstens der Feldwert)" : string.Empty);
        }
    }
}
