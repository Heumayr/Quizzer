using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews.Sub;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Tippsprint und Stoppuhr beim Spielleiter (M35 C1), auf demselben Pruefstand wie die Reaktion:
    /// das Rennen am Beamer, die Tafel, die Vorschlaege auf der Spielerkarte, Toene, Wiederholen.
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        private Task StandAsync(int telefon, int tipps, int ms, bool ende = false)
            => AbgebenAsync(telefon, new { taps = tipps, elapsedMs = ms, isFinal = ende }, MinigameKind.TapSprint);

        [TestMethod]
        public async Task TheTapSprintRaceRunsOnTheBeamerAndTheMostTapsWin()
        {
            // Drei Runden in der Spalte: eine Art ohne Runden spielt trotzdem genau eine.
            var vm = await OeffneMinispielAsync(MinigameKind.TapSprint, f => { f.DurationSeconds = 5; f.Rounds = 3; });
            var anna = world!.Players[0];
            var bert = world.Players[1];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);

            Assert.AreEqual(BuzzerControlsLayout.Minigame, vm.CurrentBuzzerLayout);
            Assert.AreEqual(1, serverVm!.BuzzerControlsViewModel!.MinigameState!.Setup.TotalRounds, "Der Tippsprint spielt mehr als eine Runde.");
            var aussen = (MinigameClientInfo)serverVm.BuzzerControlsViewModel.MinigameState.InfoFor(null);
            Assert.AreEqual(5, ((TapSprintClientInfo)aussen.KindInfo!).DurationSeconds, "Die Dauer kommt nicht beim Server an.");

            await vm.StartMinispielRundeAsync();
            Assert.IsTrue(Gespielt("Buzzer"), "Beim Start kam kein Ton.");
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Tippsprint läuft");

            // --- Das Rennen: Zahl und Balken, der Fuehrende vorn ---
            await StandAsync(0, 8, 1000);
            await StandAsync(1, 16, 1000);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Zeilen.Count == 2 && vm.Reaktion.Zeilen[0].Zeit == "16 Tipps"),
                "Das Rennen erreicht den Beamer nicht: " + string.Join(", ", vm.Reaktion.Zeilen.Select(z => z.Zeit)));
            Assert.AreEqual(bert.CalculatedDisplayName, vm.Reaktion.Zeilen[0].Name);
            Assert.AreEqual(0.5, vm.Reaktion.Zeilen[1].BalkenAnteil, 1e-9, "Der Balken folgt nicht dem Stand.");
            Assert.AreEqual(ScoreSuggestion.None, Karte(bert).Suggestion, "Ein Zwischenstand macht schon einen Vorschlag.");

            // --- Endstaende: Anna holt auf und gewinnt ---
            await StandAsync(1, 40, 5000, ende: true);
            await StandAsync(0, 52, 5000, ende: true);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")),
                "Nach dem letzten Endstand steht kein Ergebnis am Beamer: " + vm.Reaktion.Kopfzeile);
            Assert.AreEqual(anna.CalculatedDisplayName, vm.Reaktion.Zeilen[0].Name);
            Assert.AreEqual("★ Sieger", vm.Reaktion.Zeilen[0].SiegerText, "Der Sieger ist nicht mit Symbol und Text markiert.");
            Assert.AreEqual(ScoreSuggestion.Right, Karte(anna).Suggestion, "Die meisten Tipps haben keinen Vorschlag.");
            Assert.AreEqual(world.Coordinate.CurrentPoints, Karte(anna).CurrentScoreManipulation, "Nicht die Feldpunkte.");
            Assert.AreEqual(ScoreSuggestion.None, Karte(bert).Suggestion, "Der Zweite hat einen Vorschlag.");
            StringAssert.Contains(Karte(bert).ReihenfolgeText, "40 Tipps");
            Assert.IsTrue(Gespielt("Ende"), "Zum Ergebnis kam kein Ton.");
            Assert.IsTrue(vm.ShowResultWindowCalls > 0, "Nach dem Ende geht das Bewertungsfenster nicht auf.");
            Assert.IsTrue(serverVm.BuzzerControlsViewModel.LivePlayers.Any(p => p.AnswerText == "52 Tipps"),
                "Die Uebersicht des Spielleiters zeigt die Tipps nicht.");

            // --- Wiederholen: die gewertete Runde zaehlt nicht mehr ---
            await vm.WiederholeMinispielRundeAsync();

            Assert.AreEqual(ScoreSuggestion.None, Karte(anna).Suggestion, "Nach dem Wiederholen bleibt der Vorschlag stehen.");
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "Tippsprint läuft");
            Assert.IsTrue(vm.Reaktion.Zeilen.All(z => z.Zeit == "0 Tipps"), "Das Rennen beginnt nicht bei null.");
        }

        [TestMethod]
        public async Task TheStopwatchPicksTheClosestTime()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.Stopwatch, f => f.DurationSeconds = 10);
            var anna = world!.Players[0];
            var bert = world.Players[1];
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);

            await vm.StartMinispielRundeAsync();
            StringAssert.Contains(vm.Reaktion.Kopfzeile, "10 Sekunden", "Der Beamer nennt die Zielzeit nicht.");

            await AbgebenAsync(0, new { milliseconds = 10230 }, MinigameKind.Stopwatch);
            await AbgebenAsync(1, new { milliseconds = 9900 }, MinigameKind.Stopwatch);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")),
                "Nach der letzten Zeit steht kein Ergebnis am Beamer: " + vm.Reaktion.Kopfzeile);

            var oben = vm.Reaktion.Zeilen[0];
            Assert.AreEqual(bert.CalculatedDisplayName, oben.Name, "Die naechste Zeit steht nicht oben.");
            Assert.AreEqual("9,90 s (0,10 s daneben)", oben.Zeit);
            Assert.AreEqual("★ Sieger", oben.SiegerText);
            Assert.AreEqual(ScoreSuggestion.Right, Karte(bert).Suggestion, "Die naechste Zeit hat keinen Vorschlag.");
            Assert.AreEqual(ScoreSuggestion.None, Karte(anna).Suggestion, "Die weitere Zeit hat einen Vorschlag.");
            StringAssert.Contains(Karte(anna).ReihenfolgeText, "0,23 s daneben");
            Assert.IsTrue(serverVm!.BuzzerControlsViewModel!.LivePlayers.Any(p => p.AnswerText == "10,23 s"),
                "Die Uebersicht des Spielleiters zeigt die Zeit nicht.");
            Assert.IsTrue(Gespielt("Ende"), "Zum Ergebnis kam kein Ton.");
        }
    }
}
