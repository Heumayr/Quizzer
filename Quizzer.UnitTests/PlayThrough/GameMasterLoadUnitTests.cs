using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Der Einstieg des Spielleiters: <c>GameMasterViewModel.LoadModel</c> prüft, ob das Spiel
    /// überhaupt spielbar ist, bevor der Abend beginnt.
    /// <para>
    /// Bis 2026-09-06 hatte dieses ViewModel keine einzige Zusicherung - es zeigte seine vier
    /// Meldungen über <c>MessageBox.Show</c> an, und das blockiert jeden Testlauf bis zum
    /// Zeitablauf. Seit der Umstellung auf <c>UserPrompt</c> ist der Weg messbar.
    /// </para>
    /// </summary>
    [TestClass]
    public class GameMasterLoadUnitTests
    {
        private RecordingUserPrompt prompt = null!;
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
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
        /// Setzt den Moderator, den der Builder nicht setzt.
        /// <para>
        /// Bewusst am Spiel aus dem Speicher und nicht an einem frisch geladenen: ein geladenes
        /// Spiel bringt seine Mitspieler mit, und der Schreibweg stolpert dann ueber zwei
        /// Instanzen desselben <c>Player</c>.
        /// </para>
        /// </summary>
        private async Task SetModeratorAsync()
        {
            world.Game.ModeratorPlayerId = world.Players[0].Id;

            using var ctrl = new GamesController();

            await ctrl.UpdateAsync(world.Game);
            await ctrl.SaveChangesAsync();
        }

        /// <summary>
        /// Ein unbekanntes Spiel wird gemeldet, nicht stillschweigend als leer geladen.
        /// </summary>
        [TestMethod]
        public async Task AnUnknownGameIsReported()
        {
            var vm = new GameMasterViewModel();

            var geladen = await vm.LoadModel(Guid.NewGuid());

            Assert.IsNull(geladen, "Ein unbekanntes Spiel darf nicht als geladen gelten.");

            Assert.AreEqual(1, prompt.Informs.Count,
                "Der Spielleiter bekam keine Meldung - das Fenster bliebe leer, ohne Grund.");

            StringAssert.Contains(prompt.Informs[0].Message, "nicht gefunden",
                "Die Meldung sagt nicht, was los ist.");
        }

        /// <summary>
        /// Fehlt der Moderator, nennt die Meldung ihn - und zwar auf Deutsch.
        /// </summary>
        [TestMethod]
        public async Task AGameWithoutAModeratorIsRefusedWithAGermanHint()
        {
            var vm = new GameMasterViewModel();

            var geladen = await vm.LoadModel(world.Game.Id);

            Assert.IsNull(geladen, "Ohne Moderator darf das Spiel nicht starten.");

            Assert.AreEqual(1, prompt.Informs.Count, "Es kam keine Meldung.");

            StringAssert.Contains(prompt.Informs[0].Message, "Moderator",
                "Die Meldung nennt nicht, was fehlt. Sie lautete: " + prompt.Informs[0].Message);

            StringAssert.Contains(prompt.Informs[0].Caption, "starten",
                "Die Überschrift sagt nicht, worum es geht.");
        }

        /// <summary>
        /// Die Gegenrichtung: ein vollständiges Spiel wird geladen, ohne dass irgendetwas
        /// gemeldet wird. Ohne sie wären die beiden Proben oben auch dann grün, wenn
        /// <c>LoadModel</c> grundsätzlich <c>null</c> lieferte.
        /// </summary>
        [TestMethod]
        public async Task ACompleteGameLoadsWithoutComplaint()
        {
            await SetModeratorAsync();

            var vm = new GameMasterViewModel();

            var geladen = await vm.LoadModel(world.Game.Id);

            Assert.IsNotNull(geladen,
                "Ein vollständiges Spiel muss laden. Gemeldet wurde: "
                + string.Join(" | ", prompt.Informs.Select(i => i.Message)));

            Assert.AreEqual(0, prompt.Informs.Count,
                "Es kam eine Meldung, obwohl alles da ist: "
                + string.Join(" | ", prompt.Informs.Select(i => i.Message)));

            Assert.AreEqual(world.Game.Id, geladen!.Id);

            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }
    }
}
