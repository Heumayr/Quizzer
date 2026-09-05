using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Was der Spielleiter liest, bevor er einen Mitspieler entfernt.
    /// <para>
    /// <c>QuestionResult.PlayerId</c> steht auf CASCADE: mit dem Mitspieler verschwindet seine
    /// gesamte Punktehistorie aus allen Spielen, ohne Meldung und ohne Weg zurück. Bis
    /// 2026-09-06 stand in der Rückfrage nur „Die ausgewählten Spieler wirklich entfernen?" -
    /// das klingt nach einer Zeile in einer Liste, nicht nach einem Datenverlust.
    /// </para>
    /// </summary>
    [TestClass]
    public class PlayerRemovalWarningUnitTests
    {
        private RecordingUserPrompt prompt = null!;
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            // answer: false - der Test liest die Frage, er soll nichts loeschen.
            prompt = new RecordingUserPrompt(answer: false);
            UserPrompt.Current = prompt;

            TestEnvironment.ClearSwallowedExceptions();
            TestEnvironment.ClearDiscardedChanges();

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 2);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
            TestEnvironment.ClearDiscardedChanges();
        }

        /// <summary>
        /// Legt dem ersten Mitspieler Ergebniszeilen an - je eine auf einer eigenen Zelle.
        /// <para>
        /// Ein eindeutiger Index über (PlayerId, QuestionBaseId, GameId, GameGridCoordinateId)
        /// lässt je Spieler und Zelle genau ein Ergebnis zu. Mehrere Zeilen brauchen deshalb
        /// mehrere Zellen; gemessen 2026-09-06, nachdem der erste Anlauf am Index scheiterte.
        /// </para>
        /// </summary>
        private async Task AddResultsAsync(int anzahl)
        {
            using var ctrlZellen = new GameGridCoordinatesController();
            using var ctrlErgebnisse = new QuestionResultsController(ctrlZellen);

            for (var i = 0; i < anzahl; i++)
            {
                var zelle = new GameGridCoordinate
                {
                    Id = Guid.NewGuid(),
                    GameId = world.Game.Id,
                    X = i + 1,
                    Y = 0,
                    Phase = 1,
                    QuestionBaseId = world.Question.Id,
                };

                await ctrlZellen.InsertAsync(zelle);

                await ctrlErgebnisse.InsertAsync(new QuestionResult
                {
                    Id = Guid.NewGuid(),
                    PlayerId = world.Players[0].Id,
                    GameId = world.Game.Id,
                    GameGridCoordinateId = zelle.Id,
                    QuestionBaseId = world.Question.Id,
                    Score = 100,
                });
            }

            await ctrlZellen.SaveChangesAsync();
        }

        /// <summary>Lädt die Liste und wählt den ersten Mitspieler des Testspiels aus.</summary>
        private async Task<PlayersViewModel> BuildViewModelAsync()
        {
            var vm = new PlayersViewModel();

            await vm.LoadForTestAsync();

            vm.SelectedPlayers.Add(world.Players[0]);

            return vm;
        }

        /// <summary>
        /// Hängen Ergebnisse am Mitspieler, nennt die Rückfrage ihre Zahl.
        /// </summary>
        [TestMethod]
        public async Task TheQuestionNamesHowManyResultsWouldGo()
        {
            await AddResultsAsync(3);

            var vm = await BuildViewModelAsync();

            await TestEnvironment.RunCommandAsync(vm.RemovePlayerCommand);

            Assert.AreEqual(1, prompt.Confirms.Count, "Es wurde gar nicht gefragt.");

            StringAssert.Contains(prompt.Confirms[0].Message, "3 Ergebniszeilen",
                "Die Rückfrage beziffert den Verlust nicht. Sie lautete: "
                + prompt.Confirms[0].Message);

            StringAssert.Contains(prompt.Confirms[0].Message, world.Players[0].CalculatedDisplayName,
                "Die Rückfrage nennt nicht, wen es trifft.");
        }

        /// <summary>
        /// Und der Mitspieler steht danach noch da - die verneinte Rückfrage muss halten.
        /// Ohne diese Zusicherung wäre die vorige auch dann grün, wenn trotzdem gelöscht würde.
        /// </summary>
        [TestMethod]
        public async Task ANegativeAnswerLeavesThePlayerAlone()
        {
            await AddResultsAsync(2);

            var vm = await BuildViewModelAsync();

            await TestEnvironment.RunCommandAsync(vm.RemovePlayerCommand);

            using var ctrl = new PlayersController();

            Assert.IsNotNull(await ctrl.GetAsync(world.Players[0].Id),
                "Der Mitspieler wurde entfernt, obwohl die Rückfrage verneint wurde.");
        }

        /// <summary>
        /// Ohne Ergebnisse bleibt die Rückfrage schlicht - sie soll nicht mit einer Null drohen.
        /// </summary>
        [TestMethod]
        public async Task WithoutResultsTheQuestionStaysPlain()
        {
            var vm = await BuildViewModelAsync();

            await TestEnvironment.RunCommandAsync(vm.RemovePlayerCommand);

            Assert.AreEqual(1, prompt.Confirms.Count, "Es wurde gar nicht gefragt.");

            Assert.IsFalse(prompt.Confirms[0].Message.Contains("Ergebniszeilen"),
                "Ohne Ergebnisse gehoert der Zusatz nicht in die Frage: "
                + prompt.Confirms[0].Message);
        }

        /// <summary>
        /// Ist nichts ausgewählt, wird gar nicht erst gefragt.
        /// </summary>
        [TestMethod]
        public async Task WithoutASelectionNothingIsAsked()
        {
            var vm = new PlayersViewModel();

            await vm.LoadForTestAsync();

            await TestEnvironment.RunCommandAsync(vm.RemovePlayerCommand);

            Assert.AreEqual(0, prompt.Confirms.Count,
                "Es wurde gefragt, obwohl nichts ausgewählt ist.");
        }
    }
}
