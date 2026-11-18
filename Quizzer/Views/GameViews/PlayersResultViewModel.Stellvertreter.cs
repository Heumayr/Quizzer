using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Views.GameViews.Sub;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Stellvertreter-Duell auf den Spielerkarten (M37 T10): das Team des Siegers bekommt die Feldpunkte einmal, aufgeteilt
    /// (F-M37-06), als Vorschlag (E1) - wie das Siegerteam beim Tauziehen, nie über den Teamanteil der zusammengefassten Arten.
    /// Im Satz steht, wer fürs Team spielte und wie es ausging.
    /// </summary>
    public partial class PlayersResultViewModel
    {
        /// <summary>Je Karte Satz und Anteil; ohne Ergebnis räumt es Sätze und Vorschläge weg.</summary>
        internal void WerteStellvertreter(StellvertreterErgebnis? ergebnis, Func<Guid, string> teamname)
        {
            if (Coordinate is not { Game: { } spiel } zelle)
                return;

            var karten = PlayerResultContextList.Where(k => k.Player != null).ToList();
            var feldwert = PlayerResultContext.PunkteJetzt(zelle, CurrentQuestionViewModel);
            var vorschlaege = ergebnis == null
                ? new Dictionary<Guid, Punktevorschlag>()
                : Teamwertung.StellvertreterVorschlaege(spiel, karten.Select(k => (k.Result, k.Player.CalculatedDisplayName)), ergebnis, feldwert);

            string Name(Guid id) => karten.FirstOrDefault(k => k.Player.Id == id)?.Player.CalculatedDisplayName ?? "?";

            foreach (var karte in karten)
            {
                var satz = ergebnis == null ? string.Empty : Satz(spiel, karte, ergebnis, teamname, Name);
                karte.UebernimmStellvertreter(satz, vorschlaege.GetValueOrDefault(karte.Player.Id));
            }
        }

        /// <summary>„Stellvertreter für ✚ Rot: 312 ms – gewonnen“ bzw. „✚ Rot schickt Anna: verloren“; nie allein über Farbe.</summary>
        private static string Satz(Game spiel, PlayerResultContext karte, StellvertreterErgebnis ergebnis, Func<Guid, string> teamname, Func<Guid, string> name)
        {
            if (Teamwertung.TeamDes(spiel, karte.Result) is not { } team || ergebnis.VertreterVon(team) is not { } vertreter)
                return "Schaut beim Stellvertreter-Duell zu";

            var ausgang = ergebnis.Sieger.Count == 0 ? "keine Punkte" : ergebnis.IstSiegerteam(team) ? "gewonnen" : "verloren";

            return vertreter.SpielerId == karte.Player.Id
                ? $"Stellvertreter für {teamname(team)}: {ergebnis.Leistung(vertreter.SpielerId)} – {ausgang}"
                : $"{teamname(team)} schickt {name(vertreter.SpielerId)}: {ausgang}";
        }
    }
}
