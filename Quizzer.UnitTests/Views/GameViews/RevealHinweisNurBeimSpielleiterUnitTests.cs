using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
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
