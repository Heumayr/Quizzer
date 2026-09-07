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
        /// <b>Ein Raster mit leeren Zellen wird trotzdem fertig.</b>
        /// <para>
        /// Der Rasteraufbau legt für <i>jede</i> Position eine Zeile an, auch für die
        /// unbelegten. <c>IsGameFinished</c> zählte sie bis zum 2026-09-07 mit - und weil eine
        /// leere Zelle nie gespielt wird, kam die Siegerehrung nie. In der Spieldatenbank
        /// gemessen: „Test Spiel" 25 Zellen, davon <b>19 ohne Frage</b>.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AGridWithEmptyCellsStillFinishes()
        {
            var vm = new GameMasterViewModel();
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Probe", Phase = 1, SuggestedPhases = 1 };

            for (var i = 0; i < 2; i++)
            {
                spiel.GameGridCoordinates.Add(new GameGridCoordinate
                {
                    Id = Guid.NewGuid(), GameId = spiel.Id, X = i, Y = 0,
                    QuestionBaseId = Guid.NewGuid(), IsDone = true,
                });
            }

            for (var i = 0; i < 5; i++)
            {
                spiel.GameGridCoordinates.Add(new GameGridCoordinate
                {
                    Id = Guid.NewGuid(), GameId = spiel.Id, X = i, Y = 1,
                    QuestionBaseId = null,
                });
            }

            vm.Game = spiel;

            Assert.IsTrue(vm.IsGameFinished,
                "Das Spiel gilt als offen, obwohl jede Zelle MIT Frage gespielt ist - dann "
                + "kommt die Siegerehrung nie und der Spielerbildschirm bleibt beim Raster.");

            Assert.AreEqual(2, vm.GameGridCoordinatesCount,
                "Die Leiste zaehlt die leeren Zellen mit und meldet dauerhaft offene Fragen.");

            Assert.AreEqual("Alle Fragen gespielt", vm.OpenCellsText,
                "Die Leiste meldet weiterhin offene Fragen: " + vm.OpenCellsText);
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ohne sie wäre die obige auch dann grün, wenn
        /// <c>IsGameFinished</c> immer <c>true</c> lieferte - dann käme die Siegerehrung sofort
        /// nach der ersten Frage.
        /// </summary>
        [TestMethod]
        public void AnUnplayedCellKeepsTheGameOpen()
        {
            var vm = new GameMasterViewModel();
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Probe", Phase = 1, SuggestedPhases = 1 };

            spiel.GameGridCoordinates.Add(new GameGridCoordinate
            {
                Id = Guid.NewGuid(), GameId = spiel.Id, X = 0, Y = 0,
                QuestionBaseId = Guid.NewGuid(), IsDone = true,
            });

            spiel.GameGridCoordinates.Add(new GameGridCoordinate
            {
                Id = Guid.NewGuid(), GameId = spiel.Id, X = 1, Y = 0,
                QuestionBaseId = Guid.NewGuid(), IsDone = false,
            });

            vm.Game = spiel;

            Assert.IsFalse(vm.IsGameFinished,
                "Das Spiel gilt als fertig, obwohl eine belegte Zelle offen ist.");

            Assert.AreEqual("Noch eine offen", vm.OpenCellsText,
                "Die Leiste sagt nicht, dass noch etwas aussteht: " + vm.OpenCellsText);
        }

        /// <summary>
        /// Ein Raster ganz ohne Frage ist nicht „fertig" - sonst begruesste das Beamerfenster
        /// die Sieger, bevor eine einzige Frage gestellt wurde.
        /// </summary>
        [TestMethod]
        public void AGridWithoutAnyQuestionIsNotFinished()
        {
            var vm = new GameMasterViewModel();
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Leer", Phase = 1, SuggestedPhases = 1 };

            spiel.GameGridCoordinates.Add(new GameGridCoordinate
            {
                Id = Guid.NewGuid(), GameId = spiel.Id, X = 0, Y = 0, QuestionBaseId = null,
            });

            vm.Game = spiel;

            Assert.IsFalse(vm.IsGameFinished,
                "Ein Raster ohne jede Frage gilt als fertig gespielt.");
        }

        /// <summary>
        /// <b>Eine Frage im Spielfeld, die sich nicht spielen lässt, wird vor dem Start
        /// genannt.</b>
        /// <para>
        /// <b>Der Fragenprüfer hält nur das Speichern an.</b> Eine Frage kann nach dem Zuweisen
        /// ungültig werden - durch eine Typumwandlung, durch das Entfernen ihres letzten
        /// Lösungsschritts, oder weil sie aus einer Zeit vor dem Prüfer stammt. Bis zum
        /// 2026-09-07 fiel das erst auf, wenn der Spielleiter die Zelle vor Gästen öffnete.
        /// </para>
        /// <para>
        /// <b>Gemessen an der Spieldatenbank am 2026-09-07:</b> von 23 zugewiesenen Fragen
        /// wurden zwei beanstandet - „Test MC" ohne markierte Lösung und „Appre Frage", die von
        /// ihrem Profil abweicht.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task AnUnplayableQuestionIsNamedBeforeTheStart()
        {
            await BuildFourCellGridAsync(gespielt: 0);

            // Die Frage des Testspiels unspielbar machen - ueber den echten Weg: erst in
            // Multiple Choice umwandeln (der Typ verlangt eine markierte Loesung), dann die
            // Markierungen entfernen. Genau so entsteht der Fall im Betrieb.
            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.ConvertTypeAsync(world.Question.Id, QuestionType.MultipleChoice);
            }

            using (var ctrl = new QuestionBasesController())
            {
                var frage = await ctrl.GetAsync(world.Question.Id);

                foreach (var schritt in frage!.Steps)
                    schritt.IsResult = false;

                await ctrl.SaveWithStepsAsync(frage);
                await ctrl.SaveChangesAsync();
            }

            var vm = new GameMasterViewModel();

            await vm.LoadModel(world.Game.Id);

            Assert.AreEqual(1, prompt.Confirms.Count,
                "Es wurde nicht gefragt - dann faellt die unspielbare Frage erst vor den "
                + "Gaesten auf.");

            StringAssert.Contains(prompt.Confirms[0].Message, world.Question.Designation,
                "Die Rueckfrage nennt die Frage nicht beim Namen: " + prompt.Confirms[0].Message);

            StringAssert.Contains(prompt.Confirms[0].Message, "Lösung",
                "Die Rueckfrage sagt nicht, was fehlt: " + prompt.Confirms[0].Message);
        }

        /// <summary>
        /// <b>Ein Spielfeld aus lauter leeren Zellen startet nicht.</b>
        /// <para>
        /// Die Prüfung dagegen gab es - sie zählte aber <c>GameGridCoordinates.Count</c>, und
        /// der Rasteraufbau legt für <i>jede</i> Position eine Zeile an. Sie feuerte damit
        /// <b>nie</b>. Dritte Fundstelle derselben Familie nach <c>IsGameFinished</c> und
        /// <c>CalculatetThreshold</c>.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task AGridOfEmptyCellsRefusesToStart()
        {
            await BuildFourCellGridAsync(gespielt: 0);

            // Allen Zellen die Frage nehmen - so, wie ein frisch aufgebautes Raster aussieht.
            using (var ctrl = new GameGridCoordinatesController())
            {
                foreach (var zelle in (await ctrl.GetAllAsync()).Where(z => z.GameId == world.Game.Id))
                {
                    zelle.QuestionBaseId = null;

                    await ctrl.UpdateAsync(zelle);
                }

                await ctrl.SaveChangesAsync();
            }

            var vm = new GameMasterViewModel();

            var geladen = await vm.LoadModel(world.Game.Id);

            Assert.IsNull(geladen,
                "Ein Spielfeld ohne jede Frage liess sich starten - der Spielleiter steht dann "
                + "vor einem Raster, in dem sich keine Zelle oeffnen laesst.");

            Assert.AreEqual(1, prompt.Informs.Count, "Es kam keine Meldung.");

            StringAssert.Contains(prompt.Informs[0].Message, "keine Frage zugewiesen",
                "Die Meldung sagt nicht, was fehlt: " + prompt.Informs[0].Message);
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ein Spielfeld mit lauter spielbaren Fragen startet ohne
        /// Rückfrage - sonst wird sie weggeklickt, ohne gelesen zu werden.
        /// </summary>
        [TestMethod]
        public async Task AHealthyGridStartsWithoutAQuestion()
        {
            await BuildFourCellGridAsync(gespielt: 0);

            var vm = new GameMasterViewModel();

            var geladen = await vm.LoadModel(world.Game.Id);

            Assert.IsNotNull(geladen, "Das Spiel liess sich nicht oeffnen. Gemeldet wurde: "
                + string.Join(" | ", prompt.Informs.Select(i => i.Message)));

            Assert.AreEqual(0, prompt.Confirms.Count,
                "Es wurde gefragt, obwohl jede Frage spielbar ist: "
                + string.Join(" | ", prompt.Confirms.Select(c => c.Message)));
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

                    // Seit dem 2026-09-07 zaehlen nur belegte Zellen fuer Fortschritt und
                    // Phasenschwelle. Diese drei trugen bis dahin KEINE Frage - der Test
                    // stuetzte sich also auf genau den Fehler, den er nicht meinte: ein
                    // "Vier-Zellen-Raster", in dem drei Zellen nie spielbar waren.
                    QuestionBaseId = world.Question.Id,
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
