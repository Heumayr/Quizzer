using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.GameViews;
using System.Windows;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Zwei Wege, auf denen der Spielleiter mitten im Abend ein Fehlerfenster bekam.
    /// <para>
    /// Beide entstehen aus demselben Missverständnis über <c>null</c>: einmal, weil
    /// <c>Guid? == Guid.Empty</c> bei <c>null</c> <b>false</b> ist, und einmal, weil ein
    /// <c>!</c> neben einem <c>FirstOrDefault</c> nur die Warnung unterdrückt.
    /// </para>
    /// </summary>
    [TestClass]
    public class LeereZelleUndVerwaisteErgebnisseUnitTests
    {
        private RecordingUserPrompt prompt = null!;
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;

            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 2);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>
        /// <b>Eine leere Zelle sagt „Keine Frage gesetzt" statt einer Stapelspur.</b>
        /// <para>
        /// Der Rasteraufbau legt leere Zellen mit <c>QuestionBaseId = null</c> an - nicht mit
        /// <c>Guid.Empty</c>. Der Riegel verglich aber gegen <c>Guid.Empty</c>, und der gehobene
        /// Operator liefert bei <c>null</c> <c>false</c>: er wurde also gerade in dem Fall
        /// übersprungen, für den es ihn gibt.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task AnEmptyCellIsRefusedWithAReadableSentence()
        {
            var leer = new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                GameId = world.Game.Id,
                X = 9,
                Y = 9,
                QuestionBaseId = null,
            };

            using (var ctrl = new GameGridCoordinatesController())
            {
                await ctrl.InsertAsync(leer);
                await ctrl.SaveChangesAsync();
            }

            var vm = new TestableCurrentQuestionViewModel { Coordinate = leer };

            await vm.LoadForTestAsync();

            TestEnvironment.ThrowIfAnythingWasSwallowed();

            Assert.AreEqual(1, prompt.Informs.Count,
                "Es kam keine Meldung - dann laeuft der Aufbau weiter und wirft spaeter.");

            StringAssert.Contains(prompt.Informs[0].Message, "Keine Frage",
                "Die Meldung sagt nicht, was los ist: " + prompt.Informs[0].Message);
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Eine belegte Zelle wird nicht abgewiesen - sonst liesse sich
        /// keine Frage mehr oeffnen.
        /// </summary>
        [TestMethod]
        public async Task ACellWithAQuestionOpensWithoutComplaint()
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };

            await vm.LoadForTestAsync();

            TestEnvironment.ThrowIfAnythingWasSwallowed();

            Assert.AreEqual(0, prompt.Informs.Count,
                "Eine belegte Zelle wurde abgewiesen: "
                + string.Join(" | ", prompt.Informs.Select(i => i.Message)));
        }

        /// <summary>
        /// <b>Ein fehlender Buzzer-Server steht im Fragefenster.</b>
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> wurde eine Frage geöffnet, ohne dass der Server lief,
        /// stieg die Buzzer-Vorbereitung <b>stumm</b> aus. Kein Layout ging auf die Telefone,
        /// die Buzzer-Zeile blieb leer, und „Runde zurücksetzen" war tot. Das Fragefenster ist
        /// modal - der Spielleiter kam an das Buzzer-Fenster gar nicht mehr heran. Auf dem
        /// Bildschirm stand kein Grund.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task AMissingBuzzerServerIsSaidOutLoud()
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };

            await vm.LoadForTestAsync();

            // Die Standardfrage des Testspiels braucht den Buzzer, und im Testlauf laeuft er
            // nicht - genau die Lage, um die es geht.
            Assert.IsTrue(vm.BuzzerFehlt,
                "Die Probe steht gar nicht in der gemeinten Lage - laeuft hier ein "
                + "Buzzer-Server? Dann misst sie nichts.");

            StringAssert.Contains(vm.BuzzerFehltText, "Buzzer-Server",
                "Es steht kein Grund im Fenster: '" + vm.BuzzerFehltText + "'");

            StringAssert.Contains(vm.BuzzerFehltText, "schließen",
                "Der Satz sagt nicht, was zu tun ist - und das Fenster ist modal, der "
                + "Spielleiter kommt sonst nirgends hin: '" + vm.BuzzerFehltText + "'");

            Assert.AreEqual(Visibility.Visible, vm.BuzzerFehltVisibility,
                "Der Hinweis ist eingeklappt.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Eine Frage, die die Telefone gar nicht braucht, bekommt
        /// keinen Warnhinweis - sonst stünde er bei jeder Frage da und würde nicht mehr gelesen.
        /// </summary>
        [TestMethod]
        public void AQuestionWithoutPhonesShowsNoWarning()
        {
            var frage = new Quizzer.DataModels.Models.QuestionTypes.DefaultQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Ohne Telefone",
                BuzzerControlsLayout = BuzzerControlsLayout.None,
            };

            var vm = new TestableCurrentQuestionViewModel
            {
                Coordinate = new GameGridCoordinate
                {
                    Id = Guid.NewGuid(),
                    QuestionBaseId = frage.Id,
                    QuestionBase = frage,
                },
            };

            Assert.IsFalse(vm.BuzzerFehlt,
                "Auch eine Frage ohne Telefone warnt vor dem fehlenden Buzzer-Server.");

            Assert.AreEqual(Visibility.Collapsed, vm.BuzzerFehltVisibility,
                "Der Hinweis steht da, obwohl nichts fehlt.");
        }

        /// <summary>
        /// <b>Das Ergebnisfenster geht mit dem Fragefenster zu.</b>
        /// <para>
        /// <b>Gemessen 2026-09-07.</b> Es ging ohne Besitzer und ohne Dialog auf und wurde
        /// nirgends geschlossen. Wer im Fragefenster „Abschließen" und „Nächster wählt aus"
        /// drückte, ohne im Ergebnisfenster zu speichern, ließ es mit den <b>ungespeicherten
        /// Bewertungen</b> stehen. Beim nächsten Zellenklick ging ein zweites auf - und ein
        /// „Speichern" im alten schrieb die Punkte auf die <b>vorige</b> Zelle.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task TheResultWindowClosesWithTheQuestionWindow()
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };

            await vm.LoadForTestAsync();

            Assert.AreEqual(0, vm.CloseResultWindowCalls,
                "Es wurde geschlossen, bevor das Fragefenster zuging.");

            await vm.ClosedForTestAsync();

            Assert.AreEqual(1, vm.CloseResultWindowCalls,
                "Das Ergebnisfenster bleibt offen, wenn das Fragefenster zugeht - eine spaeter "
                + "gespeicherte Bewertung landet dann auf der vorigen Zelle.");
        }

        /// <summary>
        /// <b>Eine Ergebniszeile eines entfernten Mitspielers reisst „Nächster wählt aus" nicht
        /// mehr um.</b>
        /// <para>
        /// <c>GamesController</c> setzt <c>result.Player</c> aus der Mannschaft des Spiels. Wer
        /// herausgenommen wurde, steht dort nicht mehr - der Eintrag ist <c>null</c>, und das
        /// <c>!</c> daneben unterdrückt nur die Warnung. In der Spieldatenbank liegen drei
        /// solche Zeilen.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AResultOfARemovedPlayerDoesNotBreakTheWinnerList()
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };

            world.Coordinate.QuestionResults.Add(new QuestionResult
            {
                Id = Guid.NewGuid(),
                PlayerId = world.Players[0].Id,
                Player = world.Players[0],
                GameId = world.Game.Id,
                GameGridCoordinateId = world.Coordinate.Id,
                QuestionBaseId = world.Question.Id,
                CorrectAnswered = true,
            });

            // Die Zeile eines Ehemaligen: die Kennung steht noch da, die Person nicht mehr.
            world.Coordinate.QuestionResults.Add(new QuestionResult
            {
                Id = Guid.NewGuid(),
                PlayerId = Guid.NewGuid(),
                Player = null!,
                GameId = world.Game.Id,
                GameGridCoordinateId = world.Coordinate.Id,
                QuestionBaseId = world.Question.Id,
                CorrectAnswered = true,
            });

            var gewinner = vm.CoordinateCorrectedAnsweredPlayers;

            Assert.AreEqual(1, gewinner.Count,
                "Die Liste enthaelt die Zeile eines entfernten Mitspielers - der naechste Griff "
                + "darauf liest winners[0].Id und faellt mit einer NullReferenceException.");

            Assert.IsFalse(gewinner.Any(p => p == null),
                "In der Gewinnerliste steht ein leerer Eintrag.");

            Assert.AreEqual(world.Players[0].Id, gewinner[0].Id,
                "Der verbliebene Gewinner ist der falsche.");
        }
    }
}
