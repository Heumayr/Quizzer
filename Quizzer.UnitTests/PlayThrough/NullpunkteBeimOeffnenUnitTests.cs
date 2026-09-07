using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Eine Zelle, in deren Datenbankzeile 0 Punkte stehen, obwohl ihre Frage welche hat.
    /// <para>
    /// <b>Gemessen 2026-09-07 an der Spieldatenbank:</b> im Spiel „Test Spiel" standen <b>fünf
    /// von sechs</b> belegten Zellen auf <c>CurrentPoints = 0</c> — bei Fragen mit 100 bis 200
    /// Punkten. Die Punkte sind <i>gespeicherte Spalten</i>, keine berechneten Eigenschaften;
    /// <c>GameGridCoordinate.CalculateAndSetCurrentPoints</c> steigt aus, solange der
    /// Rückverweis <c>Game</c> fehlt, und lässt den gespeicherten Stand dann <b>unangetastet</b>.
    /// </para>
    /// <para>
    /// <b>Diese Zusicherung hält fest, dass ein solcher Altbestand sich beim Öffnen von selbst
    /// richtet</b> — statt am Quizabend als „0 Punkte" auf dem Beamer zu stehen.
    /// </para>
    /// <para>
    /// <b>Zur Gegenprobe, weil sie überrascht hat:</b> es reparieren <i>zwei unabhängige
    /// Stellen</i>. <c>GameMasterViewModel.LoadModel</c> ruft
    /// <c>SetPhaseAndSetCoordinatesPhase</c>, und <c>GridBuilder</c> setzt beim Zellenbau den
    /// Rückverweis und rechnet noch einmal. Baut man nur <i>eine</i> davon aus, bleibt diese
    /// Zusicherung <b>grün</b> — gemessen 2026-09-07. Rot wird sie erst, wenn beide fallen. Das
    /// ist Redundanz, kein Defekt; wer hier misst, muss es nur wissen.
    /// </para>
    /// <para>
    /// <b>Und noch eine Annahme ist dabei widerlegt worden:</b> der Rückverweis <c>Game</c> einer
    /// Zelle ist nach dem Laden <b>nicht</b> null. EF Core zieht die Navigationsbeziehung beim
    /// <c>Include</c> selbst nach; das <c>cell.Game = game</c> im <c>GridBuilder</c> ist ein
    /// Gürtel neben den Hosenträgern, nicht die einzige Quelle.
    /// </para>
    /// </summary>
    [TestClass]
    public class NullpunkteBeimOeffnenUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1);

            // Ohne Moderator steigt LoadModel vor allem anderen aus - Startbar() verlangt ihn.
            using var ctrl = new GamesController();

            var spiel = await ctrl.GetAsync(world.Game.Id);

            spiel!.ModeratorPlayerId = world.Players[0].Id;

            await ctrl.UpdateAsync(spiel);
            await ctrl.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>Setzt die gespeicherten Punkte der Testzelle auf 0.</summary>
        private async Task NulleDieZelleAsync()
        {
            using var ctrl = new GameGridCoordinatesController();

            var zelle = await ctrl.GetAsync(world.Coordinate.Id);

            Assert.IsNotNull(zelle, "Die Testzelle fehlt.");

            zelle!.CurrentPoints = 0;
            zelle.CurrentMinusPoints = 0;

            await ctrl.UpdateAsync(zelle);
            await ctrl.SaveChangesAsync();
        }

        /// <summary>Liest die gespeicherten Punkte der Testzelle.</summary>
        private async Task<(int Punkte, int Minus)> LiesDieZelleAsync()
        {
            using var ctrl = new GameGridCoordinatesController();

            var zelle = await ctrl.GetAsync(world.Coordinate.Id);

            Assert.IsNotNull(zelle, "Die Testzelle fehlt.");

            return (zelle!.CurrentPoints, zelle.CurrentMinusPoints);
        }

        /// <summary>
        /// <b>Das Öffnen rechnet die Nullen weg — und schreibt sie fest.</b>
        /// </summary>
        [TestMethod]
        public async Task OpeningTheGameRepairsStoredZeroPoints()
        {
            await NulleDieZelleAsync();

            var vorher = await LiesDieZelleAsync();

            Assert.AreEqual(0, vorher.Punkte,
                "Der Aufbau hat die Zelle gar nicht auf 0 gebracht - dann misst der Rest nichts.");

            var vm = new GameMasterViewModel();

            var geladen = await vm.LoadModel(world.Game.Id);

            Assert.IsNotNull(geladen, "Das Spiel liess sich nicht oeffnen.");

            var nachher = await LiesDieZelleAsync();

            Assert.AreNotEqual(0, nachher.Punkte,
                "Die Zelle steht nach dem Oeffnen immer noch auf 0 Punkten. Am Quizabend gaebe "
                + "eine richtige Antwort dort keine Punkte - und niemand saehe, warum.");

            // Die Formel hier NOCH EINMAL, aus den Werten der Datenbank - nicht ueber
            // CalculateAndSetCurrentPoints. Sonst pruefte die Zusicherung die Methode gegen sich
            // selbst und bliebe gruen, egal was sie rechnet.
            using var ctrlSpiel = new GamesController();

            var spiel = await ctrlSpiel.GetAsync(world.Game.Id);

            var stufe = (int)world.Question.Difficulty;

            var erwartet = world.Question.Points
                * (spiel!.DifficultyMultiplier * stufe + 1)
                + spiel.DifficultyAddition * stufe;

            erwartet = erwartet * (spiel.PhaseMultiplier * spiel.Phase)
                + spiel.PhaseAddition * spiel.Phase;

            Assert.AreEqual((int)erwartet, nachher.Punkte,
                "Die Punkte stimmen nicht mit der Formel ueberein, die im context.md steht.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Eine bereits <i>gespielte</i> Zelle behält ihre
        /// eingefrorenen Punkte — auch eine mit 0. Würde sie mitgerechnet, änderte sich der
        /// Punktestand eines längst gespielten Abends rückwirkend.
        /// </summary>
        [TestMethod]
        public async Task APlayedCellKeepsItsZero()
        {
            using (var ctrl = new GameGridCoordinatesController())
            {
                var zelle = await ctrl.GetAsync(world.Coordinate.Id);

                zelle!.CurrentPoints = 0;
                zelle.CurrentMinusPoints = 0;
                zelle.IsDone = true;

                await ctrl.UpdateAsync(zelle);
                await ctrl.SaveChangesAsync();
            }

            var vm = new GameMasterViewModel();

            await vm.LoadModel(world.Game.Id);

            var nachher = await LiesDieZelleAsync();

            Assert.AreEqual(0, nachher.Punkte,
                "Eine gespielte Zelle bekam neue Punkte - damit aendert sich der Punktestand "
                + "eines abgeschlossenen Abends rueckwirkend.");
        }
    }
}
