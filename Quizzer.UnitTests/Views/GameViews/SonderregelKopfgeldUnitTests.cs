using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.Logic.Spielablauf;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Kopfgeld im Fragefenster (M35 C3b), echter Server: keine Vorphase, die Frage geht gleich auf die Telefone, der
    /// Beamer nennt den Gesuchten mit Symbol. Je richtigem Jaeger zahlt der Gesuchte, der nicht richtig ist - auch ohne
    /// eigene Bewertung -, und die Ruecknahme dreht alle Karten zurueck.
    /// </summary>
    [TestClass]
    public class SonderregelKopfgeldUnitTests : SonderregelServerTestBasis
    {
        protected override int Port => 5431;

        [TestMethod]
        public async Task EveryRightHunterTakesTheBountyFromTheLeaderWhoIsNotRight()
        {
            ZelleEinrichten = z => z.SpecialRuleShare = 50;
            var vm = await OeffneAsync(CellSpecialRule.Bounty, spieler: 3, kontostaende: [70, 20, 20]);
            var feld = world!.Coordinate.CurrentPoints;
            var kopfgeld = Punkteanteil.Von(feld, 50);
            var (anna, bert, clara) = (Karte(vm, 0), Karte(vm, 1), Karte(vm, 2));

            Assert.IsFalse(vm.VorphaseLaeuft, "Das Kopfgeld spielt eine Vorphase.");
            Assert.AreEqual(vm.Question!.BuzzerControlsLayout, vm.CurrentBuzzerLayout, "Die Frage ging nicht gleich auf die Telefone.");
            Assert.AreEqual(Visibility.Visible, vm.Sonderregel.HinweisVisibility, "Der Beamer nennt den Gesuchten nicht.");
            Assert.AreEqual($"{SpecialRuleProfiles.Symbol} Kopfgeld auf {world.Players[0].CalculatedDisplayName}", vm.Sonderregel.Hinweis);
            Assert.AreEqual("Gesucht", anna.SonderregelText);
            Assert.AreEqual("Jäger", bert.SonderregelText);

            // --- Bert richtig: Anna zahlt, ohne bewertet zu sein ---
            bert.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(feld + kopfgeld, bert.CurrentScoreManipulation, "Der Jaeger bekommt das Kopfgeld nicht.");
            Assert.AreEqual(-kopfgeld, anna.CurrentScoreManipulation, "Die unbewertete Gesuchte zahlt nicht.");
            Assert.AreEqual(ScoreSuggestion.Correction, anna.Suggestion);

            // --- Clara auch: Anna zahlt zweimal ---
            clara.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(-2 * kopfgeld, anna.CurrentScoreManipulation, "Die Gesuchte zahlt nicht je Jaeger.");

            // --- Anna doch richtig: niemand bekommt etwas von ihr ---
            anna.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(feld, bert.CurrentScoreManipulation, "Bert behaelt das Kopfgeld, obwohl Anna richtig ist.");
            Assert.AreEqual(feld, anna.CurrentScoreManipulation);

            // --- Ruecknahme: Anna unbewertet, beide Jaeger richtig - gebucht, dann vollstaendig zurueck ---
            anna.Suggestion = ScoreSuggestion.None;
            var merk = LetzteWertung.Merke(world.Game, world.Coordinate);
            foreach (var karte in new[] { anna, bert, clara })
                karte.SetManipulationToResult();
            world.Coordinate.QuestionResults = vm.PlayersResultViewModel!.Results;

            CollectionAssert.AreEquivalent(
                new[] { (anna.Player.Id, -2 * kopfgeld), (bert.Player.Id, feld + kopfgeld), (clara.Player.Id, feld + kopfgeld) },
                merk.Aenderungen().ToList(), "Die Ruecknahme sieht das Kopfgeld nicht auf allen Karten.");
            await merk.NimmZurueckAsync(world.Game);
            Assert.IsTrue(world.Coordinate.QuestionResults.All(r => r.FinalScore == 0), "Nach der Ruecknahme steht noch ein Teil des Kopfgelds.");
        }

        [TestMethod]
        public async Task WhenAllAreTiedThereIsNoBounty()
        {
            var vm = await OeffneAsync(CellSpecialRule.Bounty, spieler: 2, kontostaende: [30, 30]);
            var feld = world!.Coordinate.CurrentPoints;

            StringAssert.Contains(vm.Sonderregel.Hinweis, "Kein Kopfgeld", "Der Beamer sagt nicht, dass das Kopfgeld entfaellt.");

            Karte(vm, 1).Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(feld, Karte(vm, 1).CurrentScoreManipulation, "Ohne Gesuchten gibt es trotzdem Kopfgeld.");
            Assert.AreEqual(0, Karte(vm, 0).CurrentScoreManipulation, "Ohne Gesuchten zahlt trotzdem jemand.");
        }
    }
}
