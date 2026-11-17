using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Views.GameViews.Minispiele;
using Quizzer.Views.GameViews.Sub;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Tauziehen auf den Spielerkarten (M37 T9): das stärkere Team bekommt die Feldpunkte einmal, aufgeteilt (F-M37-06), als
    /// Vorschlag über denselben Teamanteil wie die zusammengefassten Minispiele (<see cref="VerteileTeamanteile(IReadOnlyDictionary{Guid, int})"/>).
    /// Die Summe oder der Durchschnitt (F-M37-11) entscheidet hier nur, wer stärker zieht - der Feldwert wird nie vervielfacht.
    /// </summary>
    public partial class PlayersResultViewModel
    {
        /// <summary>Je Karte der eigene Stand und der des Teams im Satz, dazu der Anteil des Siegerteams; ein leeres Ergebnis räumt alles weg.</summary>
        internal void WerteTauziehen(TauziehenErgebnis ergebnis, IReadOnlyDictionary<Guid, TauziehenZug> zuege)
        {
            if (Coordinate is not { Game: { } spiel } zelle)
                return;

            var karten = PlayerResultContextList.Where(k => k.Player != null).ToList();
            var feldwert = PlayerResultContext.PunkteJetzt(zelle, CurrentQuestionViewModel);
            var vorschlaege = Teamwertung.TauziehenVorschlaege(
                spiel, karten.Select(k => (k.Result, k.Player.CalculatedDisplayName)), ergebnis, feldwert);

            foreach (var karte in karten)
            {
                var satz = zuege.TryGetValue(karte.Player.Id, out var zug)
                    ? Satz(zug, ergebnis, TauziehenAblauf.TeamTitel(spiel.GameTeams.FirstOrDefault(t => t.Id == zug.TeamId)))
                    : ergebnis.Teams.Count > 0 ? "Zog nicht mit – von Hand werten" : string.Empty;

                karte.UebernimmTauziehen(satz, vorschlaege.GetValueOrDefault(karte.Player.Id));
            }
        }

        /// <summary>„42 Tipps – ✚ Rot Ø 12,5 Tipps, gewonnen“; nie allein über Farbe.</summary>
        private static string Satz(TauziehenZug zug, TauziehenErgebnis ergebnis, string team)
        {
            var zugText = ergebnis.Teams.FirstOrDefault(t => t.TeamId == zug.TeamId)?.ZugText ?? string.Empty;
            var ausgang = ergebnis.IstGewinner(zug.TeamId) ? "gewonnen" : "nicht stärker";

            return $"{TippsprintEvaluator.Describe(zug.Tipps)} – {team} {zugText}, {ausgang}";
        }
    }
}
