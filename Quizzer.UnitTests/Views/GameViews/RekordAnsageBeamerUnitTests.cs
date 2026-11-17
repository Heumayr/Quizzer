using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Statistics;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.QuestionViews;
using Quizzer.Views.GameViews.QuestionViews.Typed;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// „Neuer Rekord!" auf der Minispiel-Tafel (M37 E02): die Ansage des ViewModels kommt in der Ansicht an, die Beamer und
    /// Spielleiter teilen - ohne tote Bindung, und ohne Rekord ist die Flaeche zu. Gemessen am Baum, nicht an Bildpunkten.
    /// </summary>
    [TestClass]
    public class RekordAnsageBeamerUnitTests
    {
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is T hit)
                    yield return hit;

                foreach (var deeper in Descendants<T>(child))
                    yield return deeper;
            }
        }

        /// <summary>Die Tafel der Reaktion, ausgelegt; liefert das ViewModel, an dem die Ansage haengt.</summary>
        private static (ReaktionStepView Tafel, CurrentQuestionViewModel Fenster) Tafel()
        {
            var frage = (MinigameQuestion)Factory.CreateNewQuestion(QuestionType.Minigame);
            MinigameProfiles.For(MinigameKind.Reaction).ApplyDefaults(frage);

            var fenster = new CurrentQuestionViewModel { Coordinate = new GameGridCoordinate { QuestionBase = frage } };
            var tafel = new ReaktionStepView
            {
                DataContext = new QuestionStepViewContext { Owner = fenster, Step = new QuestionStepResource { Id = Guid.NewGuid() } },
            };

            fenster.Reaktion.Zeige("Ergebnis – die beste Zeit gewinnt");
            Auslegen(tafel);
            return (tafel, fenster);
        }

        private static void Auslegen(FrameworkElement element)
        {
            element.Measure(new Size(1280, 720));
            element.Arrange(new Rect(0, 0, 1280, 720));
            element.UpdateLayout();
        }

        private static Border Rekordflaeche(ReaktionStepView tafel)
            => Descendants<Border>(tafel).Single(b => b.BorderBrush is SolidColorBrush { Color: var c } && c == Colors.Gold);

        [TestMethod]
        public void TheAnnouncementReachesTheSharedBoardWithoutDeadBindings()
        {
            UiTestHost.Run(() =>
            {
                using var wache = new BindingErrorWatch();
                var (tafel, fenster) = Tafel();

                fenster.Reaktion.ZeigeRekord(new RekordAnsage("Neuer Rekord aller Zeiten!", "Anna – 187 ms (bisher 203 ms, Bert)"));
                Auslegen(tafel);

                var flaeche = Rekordflaeche(tafel);
                var texte = Descendants<TextBlock>(flaeche).Select(t => t.Text).ToList();

                Assert.AreEqual(Visibility.Visible, flaeche.Visibility, "Die Ansage steht nicht auf der Tafel.");
                CollectionAssert.AreEqual(new[] { "Neuer Rekord aller Zeiten!", "Anna – 187 ms (bisher 203 ms, Bert)" }, texte);
                Assert.AreEqual(0, wache.Errors.Count, "Bindungen der Tafel greifen ins Leere: " + string.Join(" || ", wache.Errors));
            });
        }

        [TestMethod]
        public void WithoutARecordTheAnnouncementIsClosed()
        {
            UiTestHost.Run(() =>
            {
                var (tafel, fenster) = Tafel();
                fenster.Reaktion.ZeigeRekord(new RekordAnsage("Neuer Rekord des Abends!", "Anna – 199 ms (bisher 203 ms, Bert)"));
                fenster.Reaktion.Zeige("Runde 1 von 1 – wartet auf Grün!");
                Auslegen(tafel);

                Assert.AreEqual(Visibility.Collapsed, Rekordflaeche(tafel).Visibility, "Die Ansage steht ueber der neuen Runde.");
            });
        }
    }
}
