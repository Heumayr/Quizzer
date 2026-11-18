using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Views.GameViews.Sub;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Staffel auf den Spielerkarten (M37 T11): das Team mit den meisten Teilen bekommt die Feldpunkte einmal, aufgeteilt (F-M37-06),
    /// als Vorschlag (E1) - wie das Siegerteam beim Tauziehen, nie über den Teamanteil der zusammengefassten Arten. Im Satz stehen die
    /// Teile des Teams und wie viele das Mitglied selbst gelöst hat.
    /// </summary>
    public partial class PlayersResultViewModel
    {
        /// <summary>Je Karte Satz und Anteil; ohne Stand räumt es Sätze und Vorschläge weg.</summary>
        internal void WerteStaffel(StaffelStand? stand, Func<Guid, string> teamname)
        {
            if (Coordinate is not { Game: { } spiel } zelle)
                return;

            var karten = PlayerResultContextList.Where(k => k.Player != null).ToList();

            if (stand == null)
            {
                karten.ForEach(k => k.UebernimmStaffel(string.Empty, null));
                return;
            }

            var ergebnis = StaffelEvaluator.Werte(stand);
            var feldwert = PlayerResultContext.PunkteJetzt(zelle, CurrentQuestionViewModel);
            var vorschlaege = Teamwertung.StaffelVorschlaege(spiel, karten.Select(k => (k.Result, k.Player.CalculatedDisplayName)), ergebnis, feldwert);

            foreach (var karte in karten)
                karte.UebernimmStaffel(Satz(spiel, karte, stand, ergebnis, teamname), vorschlaege.GetValueOrDefault(karte.Player.Id));
        }

        /// <summary>„✚ Rot: 3 Teile von 8 – gewonnen, selbst 2 gelöst“; nie allein über Farbe.</summary>
        private static string Satz(Game spiel, PlayerResultContext karte, StaffelStand stand, StaffelErgebnis ergebnis, Func<Guid, string> teamname)
        {
            if (Teamwertung.TeamDes(spiel, karte.Result) is not { } team || stand.Teams.All(t => t.TeamId != team))
                return "Lief bei der Staffel nicht mit";

            var ausgang = ergebnis.Sieger.Count == 0 ? "keine Punkte" : ergebnis.IstGewinner(team) ? "gewonnen" : "verloren";
            var selbst = stand.Verlauf.Count(v => v.SpielerId == karte.Player.Id && v.Richtig);

            return $"{teamname(team)}: {StaffelTafel.Teile(stand.GeloestVon(team))} von {stand.Teile} – {ausgang}"
                   + (selbst > 0 ? $", selbst {selbst} gelöst" : string.Empty);
        }
    }
}
