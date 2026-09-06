using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views;
using System.Reflection;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Der Spielaufbau schreibt nie an der Sperre vorbei.
    /// <para>
    /// <b>B26.</b> <c>RebuildCellsAsync</c> und beide Speicherwege des Spieleditors laufen unter
    /// <c>rebuildGridLock</c>; <c>ResetGameBuildAsync</c> lief als einziger Schreibweg daran
    /// vorbei und löschte Zellen, Kopfzeilen und Zuordnungen ungeschützt. Der Fall galt als
    /// unerreichbar, weil die Rückfrage den Anlauf des Rasteraufbaus überdauert - genau so eine
    /// Begründung hielt schon einmal, bis ein Spiel sich nicht mehr öffnen ließ.
    /// </para>
    /// <para>
    /// <b>Gemessen wird der Vorgang, nicht der Endzustand.</b> „Am Ende ist das Raster leer" ist
    /// auch ohne Sperre wahr. Die Probe hält deshalb die Sperre selbst fest und sieht nach, ob
    /// das Zurücksetzen wartet - ohne Sperre löscht es einfach los.
    /// </para>
    /// </summary>
    [TestClass]
    public class SpielaufbauSperreUnitTests
    {
        private RecordingUserPrompt prompt = null!;
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 2);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        private static SemaphoreSlim Sperre(EditGameViewModel vm)
        {
            var feld = typeof(EditGameViewModel).GetField(
                "rebuildGridLock", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(feld,
                "Das Feld rebuildGridLock gibt es nicht mehr - dann misst diese Probe nichts "
                + "und muesste neu gebaut werden.");

            return (SemaphoreSlim)feld!.GetValue(vm)!;
        }

        /// <summary>Zellen des Testspiels, an denen eine Frage haengt.</summary>
        private async Task<int> BelegteZellenAsync()
        {
            using var ctrl = new GameGridCoordinatesController();

            var alle = await ctrl.GetAllAsync();

            return alle.Count(z => z.GameId == world.Game.Id && z.QuestionBaseId != null);
        }

        /// <summary>Mitspieler, die dem Testspiel zugeordnet sind.</summary>
        private async Task<int> ZuordnungenAsync()
        {
            using var ctrl = new PlayerXGamesController();

            var alle = await ctrl.GetAllAsync();

            return alle.Count(z => z.GameId == world.Game.Id);
        }

        /// <summary>
        /// Ist die Sperre genommen, löscht das Zurücksetzen nichts - es wartet.
        /// </summary>
        [TestMethod]
        public async Task ResettingWaitsWhileTheGridLockIsHeld()
        {
            var vm = new EditGameViewModel();

            await vm.LoadModel(world.Game.Id);

            var vorher = await BelegteZellenAsync();

            Assert.IsTrue(vorher > 0 && await ZuordnungenAsync() > 0,
                "Im Testspiel haengt gar keine Frage und kein Mitspieler - dann kann das "
                + "Zuruecksetzen nichts entfernen und die Probe sagt nichts.");

            var sperre = Sperre(vm);

            await sperre.WaitAsync();

            Task lauf;

            try
            {
                lauf = ((AsyncRelayCommand)vm.ResetGameBuildCommand).ExecuteAsync(null);

                await Task.WhenAny(lauf, Task.Delay(2000));

                Assert.AreEqual(1, prompt.Confirms.Count,
                    "Es wurde nicht gefragt - dann ist die Rueckfrage hinter die Sperre "
                    + "gerutscht und wartet ungelesen.");

                Assert.AreEqual(vorher, await BelegteZellenAsync(),
                    "Das Zuruecksetzen hat gelöscht, obwohl die Sperre genommen war - es laeuft "
                    + "also am Riegel vorbei, den jeder andere Schreibweg nimmt.");

                Assert.IsFalse(lauf.IsCompleted,
                    "Das Zuruecksetzen ist fertig geworden, obwohl die Sperre gehalten wird.");
            }
            finally
            {
                sperre.Release();
            }

            await lauf;

            // Nachgemessen statt angenommen: das Raster bleibt stehen und wird leer neu
            // aufgebaut - "alle Zellen weg" waere die falsche Zusicherung gewesen.
            Assert.AreEqual(0, await BelegteZellenAsync(),
                "Nach der freigegebenen Sperre haengen die Fragen noch im Raster - dann wartet "
                + "der Befehl nicht, er haengt.");

            Assert.AreEqual(0, await ZuordnungenAsync(),
                "Die Mitspieler sind noch dem Spiel zugeordnet.");
        }
    }
}
