using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews.Sub;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Bestenliste beim Spielleiter (M35 C9): nach dem Speichern stehen die gemessenen Werte am Ergebnis, auch wenn der
    /// Spielleiter die Punkte ueberschreibt; ein gespeicherter Raub zaehlt als Klau, ein verworfener nicht.
    /// </summary>
    public partial class ReaktionSpielleiterUnitTests
    {
        private async Task<List<QuestionResult>> GespeicherteErgebnisseAsync()
        {
            using var ctrl = new QuestionResultsController();
            return await ctrl.GetAllResultsForCoordinate(world!.Coordinate.Id) ?? new();
        }

        private static QuestionResult Von(List<QuestionResult> zeilen, Player spieler) => zeilen.Single(r => r.PlayerId == spieler.Id);

        /// <summary>Eine Runde Reaktion: Anna 300 ms, Bert 250 ms - Bert hat den Vorschlag „richtig".</summary>
        private async Task<TestableCurrentQuestionViewModel> ReaktionsrundeAsync()
        {
            var vm = await OeffneReaktionAsync(runden: 1);

            await vm.StartMinispielRundeAsync();
            await AbgebenAsync(0, new { falseStart = false, milliseconds = 300 });
            await AbgebenAsync(1, new { falseStart = false, milliseconds = 250 });

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.StartsWith("Ergebnis")),
                "Nach der Runde steht kein Ergebnis am Beamer: " + vm.Reaktion.Kopfzeile);
            return vm;
        }

        [TestMethod]
        public async Task AfterSavingAReactionRoundTheMeasuredTimesStandAtTheResults()
        {
            var vm = await ReaktionsrundeAsync();

            Assert.IsTrue(await vm.PlayersResultViewModel!.TrySaveAsync(), "Das Speichern scheiterte.");

            var zeilen = await GespeicherteErgebnisseAsync();
            Assert.AreEqual(300, Von(zeilen, world!.Players[0]).RecordValue, "Annas Zeit steht nicht am Ergebnis.");
            Assert.AreEqual(250, Von(zeilen, world.Players[1]).RecordValue, "Berts Zeit steht nicht am Ergebnis.");
            Assert.IsNotNull(Von(zeilen, world.Players[1]).RecordedAt, "Ohne Zeitpunkt fehlt der Liste das Datum.");
        }

        [TestMethod]
        public async Task AScoreOverriddenByTheGameMasterLeavesTheRecordAlone()
        {
            var vm = await ReaktionsrundeAsync();
            var karten = vm.PlayersResultViewModel!.PlayerResultContextList;
            PlayerResultContext Karte(Player p) => karten.Single(k => k.Player.Id == p.Id);
            var (anna, bert) = (world!.Players[0], world.Players[1]);

            Assert.AreEqual(ScoreSuggestion.Right, Karte(bert).Suggestion, "Ohne Vorschlag gibt es nichts zu ueberschreiben.");
            Karte(bert).WrongSuggestionCommand.Execute(null);
            Karte(anna).CorrectionSuggestionCommand.Execute(null);
            Karte(anna).CurrentScoreManipulation = 42;

            Assert.IsTrue(await vm.PlayersResultViewModel.TrySaveAsync(), "Das Speichern scheiterte.");

            var zeilen = await GespeicherteErgebnisseAsync();
            Assert.AreEqual((1, 250), (Von(zeilen, bert).WrongCount, Von(zeilen, bert).RecordValue),
                "Die Wertung von Hand hat den Rohwert veraendert - oder sie ist gar nicht angekommen.");
            Assert.AreEqual((42, 300), (Von(zeilen, anna).FinalScore, Von(zeilen, anna).RecordValue));
        }

        /// <summary>Wer die gespielte Zelle nur zum Nachkorrigieren wieder oeffnet, hat keine Runde gemessen - der Rekord bleibt.</summary>
        [TestMethod]
        public async Task ReopeningASavedCellWithoutARoundKeepsTheRecords()
        {
            var vm = await ReaktionsrundeAsync();
            Assert.IsTrue(await vm.PlayersResultViewModel!.TrySaveAsync(), "Das Speichern scheiterte.");

            var wieder = new TestableCurrentQuestionViewModel { Coordinate = world!.Coordinate };
            await wieder.LoadForTestAsync();
            Assert.IsTrue(await wieder.PlayersResultViewModel!.TrySaveAsync(), "Das zweite Speichern scheiterte.");

            var zeilen = await GespeicherteErgebnisseAsync();
            Assert.AreEqual((300, 250), (Von(zeilen, world.Players[0]).RecordValue, Von(zeilen, world.Players[1]).RecordValue),
                "Speichern ohne gemessene Runde hat die Rekorde geloescht.");
        }

        /// <summary>Anna bestiehlt Bert (gelingt), Bert bestiehlt Carl (Schild).</summary>
        private async Task<TestableCurrentQuestionViewModel> RaubzugAsync()
        {
            var vm = await OeffneMinispielAsync(MinigameKind.RobberAndGuard, f => f.Share = 50, spielerzahl: 3);
            var (bert, carl) = (world!.Players[1], world.Players[2]);

            await vm.StartMinispielRundeAsync();
            await AbgebenAsync(0, new { shield = false, victim = bert.Id }, MinigameKind.RobberAndGuard);
            await AbgebenAsync(1, new { shield = false, victim = carl.Id }, MinigameKind.RobberAndGuard);
            await AbgebenAsync(2, new { shield = true }, MinigameKind.RobberAndGuard);

            Assert.IsTrue(await WarteAsync(() => vm.Reaktion.Kopfzeile.Contains("Auflösung")), "Keine Auflösung am Beamer: " + vm.Reaktion.Kopfzeile);
            return vm;
        }

        [TestMethod]
        public async Task ASavedRobberyCountsAsASteal()
        {
            var vm = await RaubzugAsync();

            Assert.IsTrue(await vm.PlayersResultViewModel!.TrySaveAsync(), "Das Speichern scheiterte.");

            var zeilen = await GespeicherteErgebnisseAsync();
            CollectionAssert.AreEqual(new[] { 1, 0, 0 }, world!.Players.Select(p => Von(zeilen, p).Steals).ToList(),
                "Nur Annas gelungener Raub ist ein Klau - nicht der am Schild gescheiterte, nicht das Schild.");

            // Ein zweites Speichern zaehlt nichts doppelt - der Vorschlag ist gebucht.
            Assert.IsTrue(await vm.PlayersResultViewModel.TrySaveAsync());
            Assert.AreEqual(1, Von(await GespeicherteErgebnisseAsync(), world.Players[0]).Steals, "Der Klau zaehlt beim zweiten Speichern noch einmal.");
        }

        [TestMethod]
        public async Task ADiscardedRobberyIsNoSteal()
        {
            var vm = await RaubzugAsync();
            var anna = vm.PlayersResultViewModel!.PlayerResultContextList.Single(k => k.Player.Id == world!.Players[0].Id);

            Assert.AreEqual(ScoreSuggestion.Correction, anna.Suggestion, "Ohne Vorschlag gibt es nichts zu verwerfen.");
            anna.NoneSuggestionCommand.Execute(null);

            Assert.IsTrue(await vm.PlayersResultViewModel.TrySaveAsync(), "Das Speichern scheiterte.");

            Assert.AreEqual(0, Von(await GespeicherteErgebnisseAsync(), world!.Players[0]).Steals, "Ein verworfener Raub zaehlt als Klau.");
        }
    }
}
