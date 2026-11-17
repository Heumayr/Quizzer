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
    /// Der Spielleiter blättert bis zum Punkteverlauf (M37 E06): Sieger → Rückblick → Verlauf → Sieger. Ein altes Spiel
    /// (gespielte Zelle ohne Zeitpunkt) überspringt den Verlauf. Gerechnet aus dem Spiel im Speicher - denselben Ergebnissen,
    /// aus denen die Siegerehrung den Kontostand nimmt.
    /// </summary>
    [TestClass]
    public class PunkteverlaufSpielleiterUnitTests
    {
        [TestInitialize]
        public void SetUp() => UserPrompt.Current = new RecordingUserPrompt(answer: true);

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        /// <summary>Ein gespieltes Spiel: erster Spieler zwei richtige, Ergebnis im Speicher und in der Datenbank.</summary>
        private static async Task<GameMasterViewModel> GespieltAsync(TestGameBuilder world, DateTimeOffset? gespieltUm)
        {
            var ergebnis = new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = world.Game.Id, PlayerId = world.Players[0].Id, GameGridCoordinateId = world.Coordinate.Id,
                QuestionBaseId = world.Question.Id, RightCount = 2, Score = 300,
            };
            using (var ctrl = new QuestionResultsController())
            {
                await ctrl.InsertAsync(ergebnis);
                await ctrl.SaveChangesAsync();
            }

            world.Coordinate.QuestionResults.Add(ergebnis);
            world.Coordinate.IsDone = true;
            world.Coordinate.PlayedAt = gespieltUm;

            var leiter = new GameMasterViewModel { Game = world.Game };
            leiter.StatsContext.GameMasterViewModel = leiter;
            leiter.StatsContext.Game = world.Game;
            return leiter;
        }

        private static async Task<List<SiegerehrungTafel>> BlaettereAsync(GameMasterViewModel leiter, int mal)
        {
            var gesehen = new List<SiegerehrungTafel>();
            for (var i = 0; i < mal; i++)
            {
                await ((AsyncRelayCommand)leiter.NaechsteTafelCommand).ExecuteAsync(null);
                gesehen.Add(leiter.StatsContext.Tafel);
            }

            return gesehen;
        }

        [TestMethod]
        public async Task TheChartComesAfterTheReview()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var leiter = await GespieltAsync(world, DateTimeOffset.Now);

            var gesehen = await BlaettereAsync(leiter, 3);

            CollectionAssert.AreEqual(new[] { SiegerehrungTafel.Rueckblick, SiegerehrungTafel.Verlauf, SiegerehrungTafel.Sieger }, gesehen);
            var linie = leiter.StatsContext.Verlauf!.Linien.Single(l => l.Name == world.Players[0].CalculatedDisplayName);
            Assert.AreEqual($"{world.Players[0].CalculatedDisplayName}: 300", linie.NameText, "Der Verlauf endet nicht beim Kontostand der Siegerehrung.");
        }

        /// <summary>Gegenprobe: ein vor E06 gespieltes Spiel - kein Verlauf, der Rückblick bleibt.</summary>
        [TestMethod]
        public async Task AnOldGameSkipsTheChart()
        {
            await using var world = await TestGameBuilder.CreateAsync(normalStepCount: 1);
            var leiter = await GespieltAsync(world, gespieltUm: null);

            var gesehen = await BlaettereAsync(leiter, 2);

            CollectionAssert.AreEqual(new[] { SiegerehrungTafel.Rueckblick, SiegerehrungTafel.Sieger }, gesehen);
            Assert.IsNull(leiter.StatsContext.Verlauf);
        }
    }
}
