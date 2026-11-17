using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.Logic.Spielablauf;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Auktion im Fragefenster (M35 C3b), echter Server, echte Telefone: der Hoechstbietende (Gleichstand: der
    /// fruehere) antwortet allein, die anderen Telefone sind gesperrt, seine Karte traegt den Preis sofort, richtig
    /// zaehlt doppelt - und die Ruecknahme dreht es zurueck. Bietet niemand, spielt die Frage normal.
    /// </summary>
    [TestClass]
    public class SonderregelAuktionUnitTests : SonderregelServerTestBasis
    {
        protected override int Port => TestPorts.SonderregelAuktion;

        private async Task BieteAsync(TestableCurrentQuestionViewModel vm, int telefon, int betrag, int abgegeben)
        {
            await AbgebenAsync(telefon, CellSpecialRule.Auction, new { amount = betrag });
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft || vm.Sonderregel.Stand.Contains($"{abgegeben} von")),
                "Das Gebot kam nicht an: " + vm.Sonderregel.Stand);
        }

        [TestMethod]
        public async Task TheEarlierHighestBidderAnswersAloneAndAlwaysPays()
        {
            var vm = await OeffneAsync(CellSpecialRule.Auction, spieler: 3);
            var feld = world!.Coordinate.CurrentPoints;
            var gebot = Math.Max(1, feld / 2);
            var steuerung = serverVm!._server!.BuzzerController!;

            await BieteAsync(vm, 0, gebot, 1);
            await BieteAsync(vm, 1, gebot, 2);
            await BieteAsync(vm, 2, 0, 3);
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Nach dem letzten Gebot ist die Vorphase nicht zu.");

            // --- Anna bot zuerst: nur sie antwortet, Bert und Clara sind gesperrt und lesen es ---
            var anna = world.Players[0].Id;
            Assert.IsTrue(await WarteAsync(() => steuerung.StateManager.AnswerLock.Answerers?.SequenceEqual([anna]) == true),
                "Am Server antwortet nicht genau der Gewinner.");
            Assert.IsTrue(await WarteAsync(() => Stand(1)?.ExcludedFromQuestion == true), "Berts Telefon ist nicht gesperrt.");
            Assert.IsFalse(Stand(0)!.ExcludedFromQuestion, "Der Gewinner ist gesperrt.");
            StringAssert.Contains(vm.Sonderregel.Hinweis, world.Players[0].CalculatedDisplayName, "Der Beamer nennt den Gewinner nicht.");

            var annaKarte = Karte(vm, 0);
            Assert.AreEqual($"Gebot {gebot} – ersteigert für {gebot}", annaKarte.SonderregelText);
            Assert.AreEqual($"Gebot {gebot} – überboten", Karte(vm, 1).SonderregelText);
            Assert.AreEqual("Gepasst", Karte(vm, 2).SonderregelText);

            // --- Der Preis steht sofort auf der Karte; richtig doppelt, falsch nur der Preis ---
            Assert.AreEqual(-gebot, annaKarte.CurrentScoreManipulation, "Der Gewinner zahlt nicht schon vor der Bewertung.");
            annaKarte.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(2 * feld - gebot, annaKarte.CurrentScoreManipulation, "Richtig zaehlt nicht doppelt minus Preis.");
            annaKarte.Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-gebot, annaKarte.CurrentScoreManipulation, "Falsch kostet mehr als den Preis.");

            // Die anderen Karten bleiben unberuehrt.
            Assert.AreEqual(0, Karte(vm, 1).CurrentScoreManipulation, "Eine ueberbotene Karte traegt eine Bewegung.");

            // --- Ruecknahme: gebucht, dann vollstaendig zurueck ---
            var merk = LetzteWertung.Merke(world.Game, world.Coordinate);
            annaKarte.Suggestion = ScoreSuggestion.Right;
            annaKarte.SetManipulationToResult();
            world.Coordinate.QuestionResults = vm.PlayersResultViewModel!.Results;

            CollectionAssert.Contains(merk.Aenderungen().ToList(), (anna, 2 * feld - gebot), "Die Ruecknahme sieht die Auktion nicht.");
            await merk.NimmZurueckAsync(world.Game);
            Assert.IsTrue(world.Coordinate.QuestionResults.All(r => r.FinalScore == 0), "Nach der Ruecknahme steht noch ein Teil der Auktion.");
        }

        [TestMethod]
        public async Task WithoutAnyBidEveryoneAnswersAsUsual()
        {
            var vm = await OeffneAsync(CellSpecialRule.Auction, spieler: 2);
            var feld = world!.Coordinate.CurrentPoints;

            await BieteAsync(vm, 0, 0, 1);
            await vm.SchliesseVorphaseAsync();
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Die Vorphase laesst sich nicht schliessen.");

            Assert.IsFalse(serverVm!._server!.BuzzerController!.StateManager.AnswerLock.Active, "Ohne Gebot ist jemand gesperrt.");
            Assert.AreEqual(string.Empty, vm.Sonderregel.Hinweis, "Ohne Gebot nennt der Beamer einen Gewinner.");

            var bert = Karte(vm, 1);
            Assert.AreEqual(0, bert.CurrentScoreManipulation, "Ohne Gebot traegt eine Karte eine Bewegung.");
            bert.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(feld, bert.CurrentScoreManipulation, "Ohne Gebot zaehlen nicht die Feldpunkte.");
        }
    }
}
