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

        /// <summary>
        /// Erweitert das Testspiel auf vier Zellen und markiert die genannte Zahl als gespielt.
        /// Bei vier Zellen und zwei Phasen liegt die Schwelle nach der zweiten Runde. Moderator,
        /// Rastergröße und Rundenstand gehen in <b>einem</b> Schreibvorgang mit.
        /// <para>
        /// Bewusst nicht in zwei: <c>RowVersion</c> ist vorhanden, aber niemand behandelt einen
        /// Konflikt. Wer dasselbe Objekt aus dem Speicher zweimal hintereinander schreibt,
        /// bekommt eine <c>DbUpdateConcurrencyException</c> — gemessen 2026-09-06 beim Bau
        /// dieses Tests.
        /// </para>
        /// </summary>
        private async Task BuildFourCellGridAsync(int gespielt)
        {
            using var ctrlSpiel = new GamesController();
            using var ctrl = new GameGridCoordinatesController(ctrlSpiel);

            // Frisch laden statt die Objekte aus dem Aufbau zu benutzen: RowVersion ist als
            // Nebenläufigkeitsmarke vorhanden, aber niemand behandelt einen Konflikt. Ein Schreiben
            // über ein Objekt, dessen Marke nicht mehr stimmt, wirft eine
            // DbUpdateConcurrencyException - gemessen 2026-09-06, und zwar nur im Gesamtlauf,
            // nicht in der Klasse allein. Wer Testdaten nachträgt, lädt vorher.
            var zelle = await ctrl.GetAsync(world.Coordinate.Id);

            Assert.IsNotNull(zelle, "Die Zelle des Testspiels fehlt.");

            zelle!.IsDone = gespielt > 0;

            await ctrl.UpdateAsync(zelle);

            for (var i = 1; i < 4; i++)
            {
                await ctrl.InsertAsync(new GameGridCoordinate
                {
                    Id = Guid.NewGuid(),
                    GameId = world.Game.Id,
                    X = i,
                    Y = 0,
                    Phase = 1,
                    IsDone = i < gespielt,
                });
            }

            var spiel = await ctrlSpiel.GetAsync(world.Game.Id);

            Assert.IsNotNull(spiel, "Das Testspiel fehlt.");

            spiel!.ModeratorPlayerId = world.Players[0].Id;
            spiel.SuggestedPhases = 2;
            spiel.Height = 1;
            spiel.Width = 4;
            spiel.CurrentRound = gespielt;

            await ctrlSpiel.UpdateAsync(spiel);
            await ctrlSpiel.SaveChangesAsync();
        }

        /// <summary>
        /// Ist die Punkteschwelle erreicht, fragt das Spiel beim Öffnen nach dem Phasenwechsel.
        /// <para>
        /// Der Moment kommt mitten im Abend und war nie geprüft. Bleibt die Rückfrage aus, spielt
        /// die zweite Hälfte mit den Punkten der ersten weiter.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task ReachingTheThresholdAsksForThePhaseChange()
        {
            // Eine von vier Zellen gespielt: die nächste Runde ist die zweite, und dort liegt
            // bei zwei Phasen die Schwelle. Der Moderator wird gleich mitgesetzt.
            await BuildFourCellGridAsync(gespielt: 1);

            var vm = new GameMasterViewModel();

            var geladen = await vm.LoadModel(world.Game.Id);

            Assert.IsNotNull(geladen, "Das Spiel liess sich nicht oeffnen. Gemeldet wurde: "
                + string.Join(" | ", prompt.Informs.Select(i => i.Message)));

            Assert.AreEqual(1, prompt.Confirms.Count,
                "Beim Erreichen der Schwelle wurde nicht nach dem Phasenwechsel gefragt.");

            StringAssert.Contains(prompt.Confirms[0].Message, "Phase",
                "Die Rueckfrage handelt nicht von der Phase: " + prompt.Confirms[0].Message);

            // RecordingUserPrompt antwortet hier mit true - die Phase muss steigen.
            Assert.AreEqual(2, geladen!.Phase,
                "Die Phase wurde trotz Zustimmung nicht erhoeht.");
        }

        /// <summary>
        /// Die Gegenrichtung: liegt die Schwelle noch nicht an, wird auch nicht gefragt.
        /// Ohne sie wäre die Probe oben auch dann grün, wenn beim Öffnen immer gefragt würde.
        /// </summary>
        [TestMethod]
        public async Task BelowTheThresholdNothingIsAsked()
        {
            await BuildFourCellGridAsync(gespielt: 0);

            var vm = new GameMasterViewModel();

            var geladen = await vm.LoadModel(world.Game.Id);

            Assert.IsNotNull(geladen, "Das Spiel liess sich nicht oeffnen.");

            Assert.AreEqual(0, prompt.Confirms.Count,
                "Es wurde nach der Phase gefragt, obwohl die Schwelle nicht erreicht ist: "
                + string.Join(" | ", prompt.Confirms.Select(c => c.Message)));

            Assert.AreEqual(1, geladen!.Phase, "Die Phase wurde ungefragt erhoeht.");
        }
    }
}
