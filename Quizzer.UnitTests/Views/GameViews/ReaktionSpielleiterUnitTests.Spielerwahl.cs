using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Spielablauf;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews.Sub;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Duell, Raeuber und Waechter, Teilen oder Nehmen beim Spielleiter (M35 C2), auf demselben Pruefstand
    /// wie die Reaktion: Wahl am Telefon, Aufloesung am Beamer, Vorschlaege mit Grund auf beiden Karten
    /// (E1) - und die Ruecknahme nimmt Dieb und Opfer zurueck.
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        private int Feldpunkte(TestableCurrentQuestionViewModel vm) => PunkteJetzt(world!.Coordinate, vm);

        [TestMethod]
        public async Task TheDuelIsChosenOnThePhoneAndProposesPlusAndMinusWithAReason()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.Duel, f => { f.Rounds = 1; f.Share = 50; }, ausloeser: 0);
            var anna = world!.Players[0];
            var bert = world.Players[1];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);
            var einsatz = Punkteanteil.Von(Feldpunkte(vm), 50);

            Assert.AreEqual(System.Windows.Visibility.Visible, vm.MinispielPaarVisibility, "Die Paarwahl des Spielleiters fehlt.");
            Assert.AreEqual(anna.Id, vm.PaarWaehler?.Id, "Der Ausloeser ist nicht vorbelegt.");

            await vm.StartMinispielRundeAsync();
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "wählt einen Gegner");

            await AbgebenAsync(0, new { chosen = bert.Id }, MinigameKind.Duel);
            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains($"{anna.CalculatedDisplayName} gegen {bert.CalculatedDisplayName}")),
                "Die Gegnerwahl erreicht den Beamer nicht: " + vm.Reaktion.Kopfzeile);
            Assert.IsTrue(Gespielt("Tick"), "Zur Gegnerwahl kam kein Ton.");

            await AbgebenAsync(0, new { move = (int)SchereSteinPapier.Stein }, MinigameKind.Duel);
            await AbgebenAsync(1, new { move = (int)SchereSteinPapier.Schere }, MinigameKind.Duel);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")), "Kein Ergebnis am Beamer: " + vm.Reaktion.Kopfzeile);
            StringAssert.Contains(vm.Reaktion.Kopfzeile, $"{anna.CalculatedDisplayName} gewinnt");

            Assert.AreEqual(ScoreSuggestion.Correction, Karte(anna).Suggestion);
            Assert.AreEqual(einsatz, Karte(anna).CurrentScoreManipulation, "Der Sieger bekommt nicht den Anteil.");
            StringAssert.Contains(Karte(anna).SuggestionText, $"Duell gegen {bert.CalculatedDisplayName} gewonnen");
            Assert.AreEqual(-einsatz, Karte(bert).CurrentScoreManipulation, "Der Verlierer verliert nicht denselben Wert.");
            StringAssert.Contains(Karte(bert).SuggestionText, $"Duell gegen {anna.CalculatedDisplayName} verloren");
            Assert.IsTrue(vm.ShowResultWindowCalls > 0, "Nach dem Ende geht das Bewertungsfenster nicht auf.");
        }

        [TestMethod]
        public async Task WithoutTriggerTheGameMasterMustChooseFirst()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.ShareOrTake, _ => { });
            var bert = world!.Players[1];

            Assert.IsNull(vm.PaarWaehler, "Ein Ausloeser ist vorbelegt, obwohl keiner das Feld aufgedeckt hat.");

            await vm.StartMinispielRundeAsync();
            Assert.AreEqual(1, prompt.Informs.Count, "Ohne Ausloeser startet die Runde ohne Hinweis.");
            Assert.AreEqual(MinigamePhase.Waiting, serverVm!.BuzzerControlsViewModel!.MinigameState!.Phase);

            vm.PaarWaehler = bert;
            await vm.SetzePaarAsync();
            await vm.StartMinispielRundeAsync();

            Assert.AreEqual(MinigamePhase.Running, serverVm.BuzzerControlsViewModel.MinigameState.Phase, "Nach dem Festlegen startet die Runde nicht.");
            StringAssert.Contains(vm.Reaktion.Kopfzeile, $"{bert.CalculatedDisplayName} wählt einen Partner");
        }

        /// <summary>E1 und die Ruecknahme (B1): der Raub steht beim Dieb und beim Opfer, und beides geht zurueck.</summary>
        [TestMethod]
        public async Task TheRobberyLandsOnThiefAndVictimAndTheUndoTakesBoth()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.RobberAndGuard, f => f.Share = 50, spielerzahl: 3);
            var anna = world!.Players[0];
            var bert = world.Players[1];
            var carl = world.Players[2];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);
            var beute = Punkteanteil.Von(Feldpunkte(vm), 50);
            var merk = LetzteWertung.Merke(world.Game, world.Coordinate);

            await vm.StartMinispielRundeAsync();
            await AbgebenAsync(0, new { shield = false, victim = bert.Id }, MinigameKind.RobberAndGuard);
            await AbgebenAsync(1, new { shield = false, victim = carl.Id }, MinigameKind.RobberAndGuard);
            await AbgebenAsync(2, new { shield = true }, MinigameKind.RobberAndGuard);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("Auflösung")), "Keine Auflösung am Beamer: " + vm.Reaktion.Kopfzeile);
            Assert.AreEqual(3, vm.Reaktion.Zeilen.Count, "Nicht jeder steht mit seiner Wahl am Beamer.");

            Assert.AreEqual(beute, Karte(anna).CurrentScoreManipulation);
            StringAssert.Contains(Karte(anna).SuggestionText, $"{bert.CalculatedDisplayName} bestohlen");
            Assert.AreEqual(-2 * beute, Karte(bert).CurrentScoreManipulation, "Bert: beraubt und am Schild gescheitert.");
            StringAssert.Contains(Karte(bert).SuggestionText, $"von {anna.CalculatedDisplayName} bestohlen");
            Assert.AreEqual(beute, Karte(carl).CurrentScoreManipulation, "Das Schild bekommt Berts Zahlung nicht.");

            foreach (var karte in karten)
                karte.SetManipulationToResult();
            world.Coordinate.QuestionResults = vm.PlayersResultViewModel.Results;

            CollectionAssert.AreEquivalent(
                new[] { (anna.Id, beute), (bert.Id, -2 * beute), (carl.Id, beute) },
                merk.Aenderungen().ToList(), "Die Ruecknahme sieht nicht Dieb und Opfer.");

            await merk.NimmZurueckAsync(world.Game);

            Assert.IsTrue(world.Coordinate.QuestionResults.All(r => r.FinalScore == 0), "Nach der Ruecknahme steht noch ein Raub.");
        }
    }
}
