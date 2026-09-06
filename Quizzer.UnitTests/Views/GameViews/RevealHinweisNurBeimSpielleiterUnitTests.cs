using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels;
using Quizzer.DataModels.Questions;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews.QuestionViews;
using Quizzer.Views.GameViews.QuestionViews.Typed;
using System.Windows;
using System.Windows.Controls;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Was bei einer Aufdeckfrage ohne Bild auf dem <b>Beamer</b> steht.
    /// <para>
    /// <b>Gemessen 2026-09-07:</b> drei Meldungen schrieben direkt an das Hinweisfeld und damit
    /// an der Spielleiter-Sperre vorbei. Das Beamerfenster benutzt dieselbe Ansicht - also stand
    /// vor den Gästen mitten im Bild „Das Bild dieser Frage liegt nicht im Datenordner", ein
    /// Satz, der für den Spielleiter gedacht ist.
    /// </para>
    /// <para>
    /// Auf dem Beamer bleibt die Zeile leer. Ein neutraler Ersatztext wäre schlechter: die
    /// Mitspieler sollen gar nicht merken, dass etwas fehlt, sonst raten sie darüber statt über
    /// die Frage.
    /// </para>
    /// </summary>
    [TestClass]
    public class RevealHinweisNurBeimSpielleiterUnitTests
    {
        private static string HinweisBei(bool spielleiter)
        {
            var text = "nicht gelesen";

            UiTestHost.Run(() =>
            {
                var ansicht = new RevealStepView { IsMasterView = spielleiter };

                var frage = new RevealQuestion
                {
                    Id = Guid.NewGuid(),
                    Designation = "Ohne Bild",
                    ImageFileName = string.Empty,
                };

                frage.Steps.Add(new QuestionStepResource
                {
                    Id = Guid.NewGuid(),
                    QuestionBaseId = frage.Id,
                    SequenceNumber = 10,
                    StepText = "Schritt",
                });

                frage.CalculateOrderdSteps();

                ansicht.DataContext = new QuestionStepViewContext
                {
                    Owner = new TestableCurrentQuestionViewModel
                    {
                        Coordinate = new GameGridCoordinate
                        {
                            Id = Guid.NewGuid(),
                            QuestionBaseId = frage.Id,
                            QuestionBase = frage,
                        },
                    },
                    Step = frage.OrderedSteps.First(),
                };

                ansicht.RefreshView();

                ansicht.Measure(new Size(800, 600));
                ansicht.Arrange(new Rect(0, 0, 800, 600));
                ansicht.UpdateLayout();

                text = ((TextBlock)ansicht.FindName("Hinweis")).Text;
            });

            return text;
        }

        /// <summary>
        /// Baut eine Aufdeckfrage mit Flächen und liest die Meldung des Spielleiters auf dem
        /// n-ten Aufdeckschritt.
        /// </summary>
        private static string MeldungAufSchritt(int schrittIndex, int flaechen)
        {
            var text = "nicht gelesen";
            var bild = LegeProbebildAn();

            UiTestHost.Run(() =>
            {
                var ansicht = new RevealStepView { IsMasterView = true };

                var bereiche = Enumerable.Range(0, flaechen)
                    .Select(i => new RevealArea(0.1 * i, 0.1, 0.05, 0.05, null, i))
                    .ToList();

                var frage = new RevealQuestion
                {
                    Id = Guid.NewGuid(),
                    Designation = "Mit Flaechen",
                    ImageFileName = bild,
                    AreasJson = RevealAreas.ToJson(bereiche),
                };

                for (var i = 0; i < flaechen; i++)
                {
                    frage.Steps.Add(new QuestionStepResource
                    {
                        Id = Guid.NewGuid(),
                        QuestionBaseId = frage.Id,
                        SequenceNumber = (i + 1) * 10,
                        Designation = $"Aufdecken {i + 1}",
                    });
                }

                frage.CalculateOrderdSteps();

                var inhalt = frage.OrderedSteps
                    .Where(x => !x.IsStart && !x.IsFinish && !x.IsQuestionOnly)
                    .ToList();

                ansicht.DataContext = new QuestionStepViewContext
                {
                    Owner = new TestableCurrentQuestionViewModel
                    {
                        Coordinate = new GameGridCoordinate
                        {
                            Id = Guid.NewGuid(),
                            QuestionBaseId = frage.Id,
                            QuestionBase = frage,
                        },
                    },
                    Step = inhalt[schrittIndex],
                };

                ansicht.RefreshView();

                text = ((TextBlock)ansicht.FindName("Hinweis")).Text;
            });

            return text;
        }

        /// <summary>
        /// <b>Schon der erste Aufdeckschritt deckt etwas auf - und der letzte alles.</b>
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> gezählt wurden die Schritte <i>davor</i>. Auf dem ersten
        /// fiel damit gar nichts, und die letzte Fläche fiel erst auf dem
        /// Auflösungsbildschirm - solange geraten werden konnte, war das Bild nie ganz zu sehen.
        /// Bei drei Aufdeckschritten deckten nur zwei etwas auf.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheFirstStepAlreadyRevealsAndTheLastRevealsEverything()
        {
            StringAssert.Contains(MeldungAufSchritt(0, 3), "1 von 3",
                "Der erste Aufdeckschritt deckt nichts auf: " + MeldungAufSchritt(0, 3));

            StringAssert.Contains(MeldungAufSchritt(2, 3), "3 von 3",
                "Auf dem letzten Aufdeckschritt ist das Bild noch nicht frei - es wird erst auf "
                + "der Aufloesung ganz sichtbar, wenn niemand mehr raten darf: "
                + MeldungAufSchritt(2, 3));
        }

        /// <summary>
        /// Legt ein winziges echtes PNG im Ressourcenordner ab und liefert seinen Namen. Ohne
        /// ein ladbares Bild steigt der Aufbau vor der Schrittzaehlung aus - die Probe maesse
        /// dann nur die Meldung ueber das fehlende Bild.
        /// </summary>
        private static string LegeProbebildAn()
        {
            var name = "aufdeck-probe.png";
            var ordner = Settings.ResourceRootFolder;

            Directory.CreateDirectory(ordner);

            var pfad = Path.Combine(ordner, name);

            if (!File.Exists(pfad))
            {
                var bild = BitmapSource.Create(
                    2, 2, 96, 96, PixelFormats.Bgra32, null,
                    new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255 }, 8);

                var kodierer = new PngBitmapEncoder();

                kodierer.Frames.Add(BitmapFrame.Create(bild));

                using var strom = File.Create(pfad);

                kodierer.Save(strom);
            }

            return name;
        }

        /// <summary>Der Spielleiter erfährt, warum kein Bild da ist.</summary>
        [TestMethod]
        public void TheModeratorIsTold()
        {
            var text = HinweisBei(spielleiter: true);

            StringAssert.Contains(text, "Bild",
                "Der Spielleiter bekommt keinen Hinweis - dann steht er vor einer leeren Flaeche "
                + "und weiss nicht, ob das Absicht ist. Gefunden: '" + text + "'");
        }

        /// <summary>Und auf dem Beamer steht nichts davon.</summary>
        [TestMethod]
        public void TheAudienceSeesNothingOfIt()
        {
            Assert.AreEqual(string.Empty, HinweisBei(spielleiter: false),
                "Auf dem Beamer steht eine Meldung fuer den Spielleiter - vor den Gaesten.");
        }
    }
}
