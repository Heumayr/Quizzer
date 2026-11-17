using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Der Spielleiter blättert die Siegerehrung (M37 E3): Sieger → Rückblick → Sieger, erst nach der letzten Frage. Der
    /// Rückblick kommt aus der Testdatenbank; ist er leer, bleibt der Sieger stehen und der Spielleiter erfährt warum.
    /// </summary>
    [TestClass]
    public class SiegerehrungBlaetternUnitTests
    {
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        private static GameMasterViewModel Leiter(TestGameBuilder world)
        {
            var leiter = new GameMasterViewModel { Game = world.Game };
            leiter.StatsContext.GameMasterViewModel = leiter;
            leiter.StatsContext.Game = world.Game;
            return leiter;
        }

        private static async Task RichtigAsync(TestGameBuilder world, Player spieler, int anzahl)
        {
            using var ctrl = new QuestionResultsController();
            await ctrl.InsertAsync(new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = world.Game.Id, PlayerId = spieler.Id, GameGridCoordinateId = world.Coordinate.Id,
                QuestionBaseId = world.Question.Id, RightCount = anzahl, Score = 100 * anzahl,
            });
            await ctrl.SaveChangesAsync();
        }

        [TestMethod]
        public async Task TheGameMasterTurnsFromTheWinnerToTheReviewAndBack()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            await RichtigAsync(world, world.Players[0], 2);
            var leiter = Leiter(world);
            var stats = leiter.StatsContext;

            Assert.IsFalse(leiter.NaechsteTafelCommand.CanExecute(null), "Vor dem Spielende gibt es nichts zu blättern.");

            world.Coordinate.IsDone = true;
            Assert.IsTrue(leiter.NaechsteTafelCommand.CanExecute(null));
            Assert.AreEqual("Rückblick zeigen", stats.NaechsteTafelText);

            await ((AsyncRelayCommand)leiter.NaechsteTafelCommand).ExecuteAsync(null);

            Assert.AreEqual(SiegerehrungTafel.Rueckblick, stats.Tafel);
            StringAssert.Contains(stats.Rueckblick?.RichtigeZeile, "2 richtige Antworten");
            Assert.AreEqual("Sieger zeigen", stats.NaechsteTafelText);

            await ((AsyncRelayCommand)leiter.NaechsteTafelCommand).ExecuteAsync(null);
            Assert.AreEqual(SiegerehrungTafel.Sieger, stats.Tafel, "Nach dem Rückblick kommt wieder der Sieger.");
            Assert.AreEqual(0, prompt.Informs.Count);
        }

        /// <summary>Gegenprobe: ein Spiel ohne alles, was der Rückblick zeigt - der Sieger bleibt, mit einem Satz dazu.</summary>
        [TestMethod]
        public async Task WithoutAnythingToLookBackOnTheWinnerStays()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var leiter = Leiter(world);
            world.Coordinate.IsDone = true;

            await ((AsyncRelayCommand)leiter.NaechsteTafelCommand).ExecuteAsync(null);

            Assert.AreEqual(SiegerehrungTafel.Sieger, leiter.StatsContext.Tafel);
            Assert.AreEqual(1, prompt.Informs.Count, "Der Knopf tut nichts, und der Spielleiter erfährt nicht warum.");
            StringAssert.Contains(prompt.Informs[0].Message, "keinen Rückblick");
        }
    }
}
