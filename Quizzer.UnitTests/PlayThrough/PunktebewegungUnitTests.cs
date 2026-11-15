using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.Views.GameViews.Sub;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// M35 B1: Minus-Vorschläge auf der Spielerkarte folgen der Einstellung „Kontostand darf unter 0
    /// fallen" (E2); ein Vorschlag trägt seinen Grund, der eigene Wert des Spielleiters bleibt frei (E1).
    /// </summary>
    [TestClass]
    public class PunktebewegungUnitTests
    {
        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        private static async Task<PlayerResultContext> KarteAsync(TestGameBuilder world, bool unterNullErlaubt, int schonGebucht = 0)
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();
            var karte = vm.PlayersResultViewModel!.PlayerResultContextList[0];
            karte.Coordinate!.Game.AllowNegativeScore = unterNullErlaubt;
            karte.Result.Score = schonGebucht;
            return karte;
        }

        [TestMethod]
        public async Task AWrongAnswerTakesAPlayerBelowZeroWhenTheGameAllowsIt()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var karte = await KarteAsync(world, unterNullErlaubt: true);

            karte.WrongSuggestionCommand.Execute(null);

            Assert.IsTrue(karte.Coordinate!.CurrentMinusPoints > 0, "Die Testfrage braucht Minuspunkte.");
            Assert.AreEqual(-karte.Coordinate.CurrentMinusPoints, karte.CurrentScoreManipulation);
        }

        [TestMethod]
        public async Task AWrongAnswerStopsAtTheBalanceWhenTheGameForbidsIt()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var leer = await KarteAsync(world, unterNullErlaubt: false);
            leer.WrongSuggestionCommand.Execute(null);
            Assert.AreEqual(0, leer.CurrentScoreManipulation);

            var mitWenig = await KarteAsync(world, unterNullErlaubt: false, schonGebucht: 30);
            mitWenig.WrongSuggestionCommand.Execute(null);
            Assert.AreEqual(-Math.Min(30, mitWenig.Coordinate!.CurrentMinusPoints), mitWenig.CurrentScoreManipulation);
        }

        [TestMethod]
        public async Task AProposalCarriesItsReasonAndFollowsTheSetting()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var verboten = await KarteAsync(world, unterNullErlaubt: false);
            verboten.SchlageVor(-150, "Klau durch Anna");

            Assert.AreEqual(PlayerResultContext.ScoreSuggestion.Correction, verboten.Suggestion);
            Assert.AreEqual(0, verboten.CurrentScoreManipulation);
            StringAssert.Contains(verboten.SuggestionText, "Klau durch Anna");

            var erlaubt = await KarteAsync(world, unterNullErlaubt: true);
            erlaubt.SchlageVor(-150, "Klau durch Anna");
            Assert.AreEqual(-150, erlaubt.CurrentScoreManipulation);
        }

        /// <summary>Der Spielleiter hat die Hand: was er selbst einträgt, kappt niemand.</summary>
        [TestMethod]
        public async Task TheGameMastersOwnValueStaysFree()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var karte = await KarteAsync(world, unterNullErlaubt: false);

            karte.CorrectionSuggestionCommand.Execute(null);
            karte.CurrentScoreManipulation = -80;

            Assert.AreEqual(-80, karte.CurrentScoreManipulation);
            Assert.IsNull(karte.Grund);
        }
    }
}
