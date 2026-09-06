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
    /// Was der Spielleiter liest, bevor er ein Spiel entfernt.
    /// <para>
    /// <b>Der dritte und letzte unbezifferte Löschweg.</b> Beim Mitspieler und bei der Frage
    /// beziffert die Rückfrage seit dem 2026-09-06, was verlorengeht; beim Spiel stand bis
    /// hierher nur „Die ausgewählten Spiele wirklich entfernen?" - nicht einmal, welche.
    /// </para>
    /// <para>
    /// Dabei ist es der teuerste der drei: <c>GamesController.BeforeActionAsync</c> räumt vor dem
    /// Löschen Ergebnisse, Zellen, Kopfzeilen und Zuordnungen selbst weg, weil die
    /// Fremdschlüssel auf NO ACTION stehen. Mit dem Spiel geht der <b>gesamte Punktestand des
    /// Abends</b>, ohne Weg zurück.
    /// </para>
    /// </summary>
    [TestClass]
    public class GameRemovalWarningUnitTests
    {
        private RecordingUserPrompt prompt = null!;
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            // answer: false - der Test liest die Frage, er soll nichts loeschen.
            prompt = new RecordingUserPrompt(answer: false);
            UserPrompt.Current = prompt;

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 2);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        private async Task AddResultsAsync(int anzahl)
        {
            using var ctrl = new QuestionResultsController();

            for (var i = 0; i < anzahl; i++)
            {
                await ctrl.InsertAsync(new QuestionResult
                {
                    Id = Guid.NewGuid(),
                    PlayerId = world.Players[i % world.Players.Count].Id,
                    GameId = world.Game.Id,
                    GameGridCoordinateId = world.Coordinate.Id,
                    QuestionBaseId = world.Question.Id,
                    Score = 100,
                });
            }

            await ctrl.SaveChangesAsync();
        }

        private async Task<GamesViewModel> BuildViewModelAsync()
        {
            var vm = new GamesViewModel();

            await vm.LoadForTestAsync();

            vm.SelectedGames.Add(world.Game);

            return vm;
        }

        /// <summary>
        /// Hängen Ergebnisse am Spiel, nennt die Rückfrage ihre Zahl - und den Namen des Spiels.
        /// </summary>
        [TestMethod]
        public async Task TheQuestionNamesTheGameAndWhatWouldGo()
        {
            await AddResultsAsync(2);

            var vm = await BuildViewModelAsync();

            await TestEnvironment.RunCommandAsync(vm.RemoveGameCommand);

            Assert.AreEqual(1, prompt.Confirms.Count, "Es wurde gar nicht gefragt.");

            StringAssert.Contains(prompt.Confirms[0].Message, world.Game.Designation,
                "Die Rueckfrage nennt nicht, welches Spiel es trifft: " + prompt.Confirms[0].Message);

            StringAssert.Contains(prompt.Confirms[0].Message, "2 Ergebniszeilen",
                "Die Rueckfrage beziffert den Verlust nicht: " + prompt.Confirms[0].Message);
        }

        /// <summary>
        /// Und das Spiel steht danach noch da. Ohne diese Zusicherung wäre die vorige auch dann
        /// grün, wenn trotz verneinter Rückfrage gelöscht würde.
        /// </summary>
        [TestMethod]
        public async Task ANegativeAnswerLeavesTheGameAlone()
        {
            await AddResultsAsync(1);

            var vm = await BuildViewModelAsync();

            await TestEnvironment.RunCommandAsync(vm.RemoveGameCommand);

            using var ctrl = new GamesController();

            Assert.IsNotNull(await ctrl.GetAsync(world.Game.Id),
                "Das Spiel wurde entfernt, obwohl die Rueckfrage verneint wurde.");
        }

        /// <summary>
        /// Ohne gespielte Runde bleibt die Rückfrage schlicht - sie soll nicht mit einer Null
        /// drohen. Die Gegenrichtung zur ersten Zusicherung: der Zusatz darf nicht immer
        /// erscheinen.
        /// </summary>
        [TestMethod]
        public async Task WithoutResultsTheQuestionStaysPlain()
        {
            var vm = await BuildViewModelAsync();

            await TestEnvironment.RunCommandAsync(vm.RemoveGameCommand);

            Assert.AreEqual(1, prompt.Confirms.Count, "Es wurde gar nicht gefragt.");

            Assert.IsFalse(prompt.Confirms[0].Message.Contains("Ergebniszeile"),
                "Ohne gespielte Runde gehoert der Zusatz nicht in die Frage: "
                + prompt.Confirms[0].Message);

            StringAssert.Contains(prompt.Confirms[0].Message, world.Game.Designation,
                "Auch die schlichte Rueckfrage muss sagen, welches Spiel gemeint ist.");
        }

        /// <summary>
        /// Ist nichts ausgewählt, wird gar nicht erst gefragt.
        /// </summary>
        [TestMethod]
        public async Task WithoutASelectionNothingIsAsked()
        {
            var vm = new GamesViewModel();

            await vm.LoadForTestAsync();

            await TestEnvironment.RunCommandAsync(vm.RemoveGameCommand);

            Assert.AreEqual(0, prompt.Confirms.Count,
                "Es wurde gefragt, obwohl nichts ausgewaehlt ist.");
        }
    }
}
