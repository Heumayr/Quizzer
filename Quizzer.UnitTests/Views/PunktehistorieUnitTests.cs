using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Die Punktehistorie im Mitspieler-Fenster.
    /// <para>
    /// <b>Gemessen 2026-09-07: sie war strukturell leer</b>, aus drei voneinander unabhängigen
    /// Gründen. „Punkte gesamt" stand immer auf 0, das Raster darunter war leer - es gab keinen
    /// Zustand, in dem dort etwas stand. Der Spielleiter, der vor dem Abend nachsehen will, was
    /// jemand bisher hat, bekam eine <b>falsche Zahl</b> statt einer leeren Anzeige.
    /// </para>
    /// </summary>
    [TestClass]
    public class PunktehistorieUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 2);

            using var ctrl = new QuestionResultsController();

            await ctrl.InsertAsync(new QuestionResult
            {
                Id = Guid.NewGuid(),
                PlayerId = world.Players[0].Id,
                GameId = world.Game.Id,
                GameGridCoordinateId = world.Coordinate.Id,
                QuestionBaseId = world.Question.Id,
                Score = 300,
                MinusScore = 100,
                CorrectAnswered = true,
            });

            await ctrl.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>
        /// <b>Die Ergebnisse kommen beim Laden mit</b> - samt Spiel und Frage, denn beide Spalten
        /// des Rasters brauchen sie.
        /// </summary>
        [TestMethod]
        public async Task TheScoreHistoryIsLoadedWithThePlayer()
        {
            using var ctrl = new PlayersController();

            var spieler = await ctrl.GetAsync(world.Players[0].Id);

            Assert.IsNotNull(spieler, "Der Mitspieler liess sich nicht laden.");

            Assert.AreEqual(1, spieler!.CurrentQuestionResults.Count,
                "Die Punktehistorie ist leer - 'Punkte gesamt' zeigt damit immer 0, egal wie "
                + "viel jemand erzielt hat.");

            Assert.AreEqual(200, spieler.FinalScore,
                "Die Gesamtpunktzahl stimmt nicht: 300 Punkte minus 100 Minuspunkte.");

            var zeile = spieler.CurrentQuestionResults[0];

            Assert.IsNotNull(zeile.Game,
                "Das Spiel fehlt - die Spalte 'Spiel' bliebe leer.");

            Assert.IsNotNull(zeile.QuestionBase,
                "Die Frage fehlt - die Spalte 'Frage' bliebe leer.");
        }

        private static IEnumerable<DependencyObject> Alle(DependencyObject wurzel)
        {
            var anzahl = VisualTreeHelper.GetChildrenCount(wurzel);

            for (var i = 0; i < anzahl; i++)
            {
                var kind = VisualTreeHelper.GetChild(wurzel, i);

                yield return kind;

                foreach (var tiefer in Alle(kind))
                    yield return tiefer;
            }
        }

        /// <summary>
        /// <b>Jede Spalte des Rasters bindet auf eine Eigenschaft, die es gibt.</b>
        /// <para>
        /// Drei der sechs hießen bis 2026-09-07 <c>Question.Designation</c>, <c>Points</c> und
        /// <c>MinusPoints</c> - an <c>QuestionResult</c> gibt es keine davon. Sie blieben leer,
        /// ohne dass etwas warf; eine Bindung ins Leere ist in WPF still.
        /// </para>
        /// </summary>
        [TestMethod]
        public void EveryColumnBindsToAnExistingProperty()
        {
            var pfade = new List<string>();

            UiTestHost.Run(() =>
            {
                using var wegraeumen = new FensterAufraeumer(typeof(EditPlayerView));

                var inhalt = (FrameworkElement)wegraeumen.Fenster.Content;

                inhalt.Measure(new Size(1000, 800));
                inhalt.Arrange(new Rect(0, 0, 1000, 800));
                inhalt.UpdateLayout();

                foreach (var raster in Alle(inhalt).OfType<DataGrid>())
                {
                    foreach (var spalte in raster.Columns)
                    {
                        var bindung = spalte switch
                        {
                            DataGridTextColumn t => t.Binding as Binding,
                            DataGridCheckBoxColumn c => c.Binding as Binding,
                            _ => null,
                        };

                        if (bindung?.Path?.Path is { Length: > 0 } pfad)
                            pfade.Add(pfad);
                    }
                }
            });

            Assert.IsTrue(pfade.Count >= 6,
                $"Es wurden nur {pfade.Count} Spalten gefunden - die Probe misst nichts.");

            var typ = typeof(QuestionResult);
            var fehlend = new List<string>();

            foreach (var pfad in pfade)
            {
                var erstes = pfad.Split('.')[0];

                if (typ.GetProperty(erstes) == null)
                    fehlend.Add(pfad);
            }

            Assert.AreEqual(0, fehlend.Count,
                "Diese Spalten binden auf Eigenschaften, die es an QuestionResult nicht gibt - "
                + "sie bleiben dauerhaft leer: " + string.Join(", ", fehlend));
        }
    }
}
