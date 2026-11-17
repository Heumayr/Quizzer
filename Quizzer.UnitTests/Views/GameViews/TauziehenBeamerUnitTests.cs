using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.QuestionViews;
using Quizzer.Views.GameViews.QuestionViews.Typed;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Das Seil des Tauziehens auf der Minispiel-Tafel (M37 T9): es kommt in der Ansicht an, die Beamer und Spielleiter teilen -
    /// beide Teams mit Symbol und Name, der Knoten neben der Mitte, die Lage als Satz, ohne tote Bindung. Jede neue Tafel nimmt
    /// es weg. Gemessen am Baum, nicht an Bildpunkten.
    /// </summary>
    [TestClass]
    public class TauziehenBeamerUnitTests
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

        private static void Auslegen(FrameworkElement element)
        {
            element.Measure(new Size(1280, 720));
            element.Arrange(new Rect(0, 0, 1280, 720));
            element.UpdateLayout();
        }

        private static (ReaktionStepView Tafel, CurrentQuestionViewModel Fenster) Tafel()
        {
            var frage = (MinigameQuestion)Factory.CreateNewQuestion(QuestionType.Minigame);
            MinigameProfiles.For(MinigameKind.TugOfWar).ApplyDefaults(frage);

            var fenster = new CurrentQuestionViewModel { Coordinate = new GameGridCoordinate { QuestionBase = frage } };
            var tafel = new ReaktionStepView
            {
                DataContext = new QuestionStepViewContext { Owner = fenster, Step = new QuestionStepResource { Id = Guid.NewGuid() } },
            };

            return (tafel, fenster);
        }

        [TestMethod]
        public void TheRopeReachesTheSharedBoardWithoutDeadBindingsAndLeavesWithTheNextBoard()
        {
            UiTestHost.Run(() =>
            {
                using var wache = new BindingErrorWatch();
                var (tafel, fenster) = Tafel();
                var seil = new TauziehenSeil("✚ Rot", "★ Blau", 0.5, "★ Blau zieht stärker");

                fenster.Reaktion.Zeige("Tauziehen läuft – 15 Sekunden, zieht!", [new ReaktionTafelZeile("★ Blau", "20 Tipps (2 ziehen)", false, 1)]);
                fenster.Reaktion.ZeigeSeil(seil);
                Auslegen(tafel);

                var flaeche = (Grid)tafel.FindName("Seil");
                var texte = Descendants<TextBlock>(flaeche).Select(t => t.Text).ToList();
                var knoten = Descendants<Border>(flaeche).Single(b => b.RenderTransform is TranslateTransform);

                Assert.AreEqual(Visibility.Visible, flaeche.Visibility, "Das Seil steht nicht auf der Tafel.");
                CollectionAssert.AreEqual(new[] { "✚ Rot", "★ Blau", "★ Blau zieht stärker" }, texte, "Teams oder Lage fehlen am Seil.");
                Assert.AreEqual(seil.Versatz, ((TranslateTransform)knoten.RenderTransform).X, 1e-9, "Der Knoten steht nicht neben der Mitte.");
                Assert.AreEqual(0, wache.Errors.Count, "Bindungen des Seils greifen ins Leere: " + string.Join(" || ", wache.Errors));

                fenster.Reaktion.Zeige("Minispiel abgebrochen – diese Runde zählt nicht");
                Auslegen(tafel);
                Assert.AreEqual(Visibility.Collapsed, flaeche.Visibility, "Das Seil steht über der neuen Tafel.");
            });
        }
    }
}
