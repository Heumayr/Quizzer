using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews.Sub;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Jeder Speicherweg der Spielfenster muss auch wirklich schreiben.
    /// <para>
    /// Ein vergessenes <c>SaveChangesAsync</c> ist der teuerste Fehler dieses Projekts, weil er
    /// nichts hinterlaesst: keine Ausnahme, keine Meldung, kein roter Test. Der Spielleiter
    /// vergibt Punkte, das Fenster schliesst, und beim naechsten Aufbau stehen wieder die alten
    /// Werte da. Genau das hat der Nutzer am 05.09. als "strukturelle Probleme, vor allem beim
    /// Speichern" gemeldet.
    /// </para>
    /// <para>
    /// <c>UnsavedChangesWatch</c> meldet seit dem 06.09. jeden Controller, der mit offenen
    /// Aenderungen entsorgt wird. Dieser Test faehrt die Wege ab und sieht die Meldung nach.
    /// </para>
    /// </summary>
    [TestClass]
    public class PersistenceWatchUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 2, playerCount: 3);

            // Der Aufbau selbst schreibt viel; erst ab hier wird gemessen.
            TestEnvironment.ClearDiscardedChanges();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
            TestEnvironment.ClearDiscardedChanges();
        }

        /// <summary>
        /// Der Weg, den der Spielleiter je Zelle geht: Frage aufdecken, Punkte vergeben,
        /// Zelle abschliessen.
        /// </summary>
        [TestMethod]
        public async Task ThePathThroughOneCellLosesNothing()
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            await ((AsyncRelayCommand)vm.NextStepCommnad).ExecuteAsync(null);

            var ergebnisse = vm.PlayersResultViewModel;

            Assert.IsNotNull(ergebnisse, "Ohne Ergebnisansicht misst dieser Test nichts.");

            foreach (var karte in ergebnisse!.PlayerResultContextList)
            {
                karte.Suggestion = PlayerResultContext.ScoreSuggestion.Right;
            }

            Assert.IsTrue(await ergebnisse.TrySaveAsync(), "Das Speichern der Punkte scheiterte.");

            await ((AsyncRelayCommand)vm.SaveIsDoneFinishStateCommand).ExecuteAsync(null);

            TestEnvironment.ThrowIfAnythingWasSwallowed();
            TestEnvironment.ThrowIfAnythingWasDiscarded();
        }

        /// <summary>
        /// Und die Punkte stehen danach wirklich in der Datenbank - nicht nur im Speicher.
        /// Ohne diese Nachfrage waere der Test auch dann gruen, wenn gar nichts geschrieben
        /// wuerde: wo nichts offen ist, meldet der Waechter auch nichts.
        /// </summary>
        [TestMethod]
        public async Task TheScoresAreReallyInTheDatabase()
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            var ergebnisse = vm.PlayersResultViewModel;

            Assert.IsNotNull(ergebnisse);

            foreach (var karte in ergebnisse!.PlayerResultContextList)
            {
                karte.Suggestion = PlayerResultContext.ScoreSuggestion.Right;
            }

            Assert.IsTrue(await ergebnisse.TrySaveAsync());

            using var ctrl = new QuestionResultsController();

            var geschrieben = (await ctrl.GetAllAsync())
                .Where(r => r.GameGridCoordinateId == world.Coordinate.Id)
                .ToList();

            Assert.AreEqual(world.Players.Count, geschrieben.Count,
                "Es steht nicht je Mitspieler eine Ergebniszeile in der Datenbank.");

            Assert.IsTrue(geschrieben.All(r => r.Score > 0),
                "Die Zeilen sind da, tragen aber keine Punkte - dann wurde der Wert nicht mitgeschrieben.");

            TestEnvironment.ThrowIfAnythingWasDiscarded();
        }

        /// <summary>
        /// Die Gegenprobe zum Waechter selbst: ein Controller, der absichtlich nicht sichert,
        /// muss gemeldet werden. Ohne sie waere nicht zu unterscheiden, ob die Wege sauber sind
        /// oder ob der Waechter gar nicht angeschlossen ist.
        /// </summary>
        [TestMethod]
        public async Task TheWatchReportsAForgottenSave()
        {
            using (var ctrl = new PlayersController())
            {
                var spieler = world.Players[0];
                spieler.DisplayName = "Umbenannt ohne Sicherung";

                await ctrl.UpdateAsync(spieler);

                // Absichtlich kein SaveChangesAsync.
            }

            lock (TestEnvironment.DiscardedChanges)
            {
                Assert.AreNotEqual(0, TestEnvironment.DiscardedChanges.Count,
                    "Der Waechter ist in dieser Testsammlung nicht angeschlossen - dann sagt "
                    + "sein Schweigen in den anderen Tests nichts aus.");
            }

            TestEnvironment.ClearDiscardedChanges();
        }
    }
}
