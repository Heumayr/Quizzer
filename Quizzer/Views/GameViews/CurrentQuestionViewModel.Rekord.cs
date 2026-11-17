using Quizzer.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Statistics;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// „Neuer Rekord!" am Beamer (M37 E02): am Ende eines Minispiels gehen die Messwerte der Karten gegen die Bestenliste
    /// der lokalen Datenbank ohne diese Zelle - des Abends und aller Zeiten. Das Bewertungsfenster ist da schon offen,
    /// die Ansage haelt nichts an; sie steht auf der Tafel, die Beamer und Spielleiter teilen.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>Prueft die Messwerte der Karten und bringt einen geschlagenen Bestwert mit Ton auf die Tafel.</summary>
        private async Task PruefeRekordAsync()
        {
            if (Coordinate is not { } zelle || Minispiel is not { } frage || PlayersResultViewModel is not { } karten)
                return;

            var messwerte = karten.PlayerResultContextList
                .Where(k => k.Messwert.HasValue)
                .ToDictionary(k => k.Player.Id, k => k.Messwert.GetValueOrDefault());

            // Arten ohne Bestwert melden keine Messwerte - dann fragt auch keiner die Datenbank.
            if (messwerte.Count == 0)
                return;

            List<LeaderboardRow> bisher;
            using (var ctrl = new QuestionResultsController())
                bisher = await ctrl.BestenlistenZeilenAsync();

            var rekorde = Rekordpruefung.Pruefe(bisher, messwerte, new RekordZelle(zelle.GameId, zelle.Id, frage.Kind, frage.Variant));
            var namen = MinispielSpieler().ToDictionary(s => s.Id, s => s.Name);

            if (Rekordpruefung.Ansage(rekorde, id => namen.GetValueOrDefault(id, "?")) is not { } ansage)
                return;

            Reaktion.ZeigeRekord(ansage);
            Klang.Rekord();
        }
    }
}
