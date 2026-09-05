using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.GameViews;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Die beiden Fenster, die der Spielleiter waehrend eines Spielzugs bedient, wirklich
    /// aufgebaut. Eine Zusicherung auf die Properties allein wuerde eine tote Bindung nicht
    /// bemerken - und genau eine solche stand bis 2026-09-06 im Spielleiter-Fenster.
    /// </summary>
    [TestClass]
    public class GameWindowsRenderUnitTests
    {
        /// <summary>
        /// Fuehrt die Arbeit auf dem gemeinsamen Oberflaechen-Thread aus.
        /// <para>
        /// Frueher legte diese Klasse einen eigenen STA-Thread an und fuhr dessen Dispatcher
        /// am Ende herunter. <c>Application.Current</c> ist prozessweit - danach fand keine
        /// spaetere Klasse mehr einen lebenden Dispatcher. Einzelheiten in
        /// <see cref="UiTestHost"/>.
        /// </para>
        /// </summary>
        private static void OnUiThread(Action action) => UiTestHost.Run(action);

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            var count = VisualTreeHelper.GetChildrenCount(root);

            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is T hit)
                    yield return hit;

                foreach (var deeper in Descendants<T>(child))
                    yield return deeper;
            }
        }

        /// <summary>Sichtbar heisst: ausgelegt und kein Vorfahre ausgeblendet.</summary>
        private static bool IsLaidOutAndVisible(FrameworkElement element)
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
                return false;

            DependencyObject? current = element;

            while (current != null)
            {
                if (current is UIElement ui && ui.Visibility != Visibility.Visible)
                    return false;

                current = VisualTreeHelper.GetParent(current);
            }

            return true;
        }

        private static List<string> VisibleTexts(FrameworkElement root) =>
            Descendants<TextBlock>(root).Where(IsLaidOutAndVisible).Select(t => t.Text)
                .Concat(Descendants<Button>(root).Where(IsLaidOutAndVisible)
                    .Select(b => b.Content?.ToString() ?? string.Empty))
                .Concat(Descendants<Label>(root).Where(IsLaidOutAndVisible)
                    .Select(l => l.Content?.ToString() ?? string.Empty))
                .ToList();

        private static FrameworkElement Layout(Window window, double breite = 1200, double hoehe = 700)
        {
            var content = (FrameworkElement)window.Content;

            content.Measure(new Size(breite, hoehe));
            content.Arrange(new Rect(0, 0, breite, hoehe));
            content.UpdateLayout();

            return content;
        }

        /// <summary>Eine Schaetzfrage samt Zelle und Spiel, wie das Spiel sie oeffnet.</summary>
        private static CurrentQuestionViewModel BuildQuestionViewModel()
        {
            var question = new AppreciateQestion
            {
                Id = Guid.NewGuid(),
                Designation = "Grossglockner",
                DesignationShort = "Berg",
                QuestionText = "Wie hoch ist der Großglockner?",
                Difficulty = Difficulty.Level2,
                Notes = "Nicht mit dem Ortler verwechseln.",
                ValueKind = AppreciateValueKind.Length,
                Unit = AppreciateUnit.Meter,
                ExpectedValue = 3798,
            };

            question.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = question.Id,
                StepText = "Der höchste Berg Österreichs.",
                SequenceNumber = 10,
            });

            question.CalculateOrderdSteps();

            var game = new Game { Id = Guid.NewGuid(), Designation = "Testabend", Phase = 2, SuggestedPhases = 4 };

            var coordinate = new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                Game = game,
                Phase = 2,
                QuestionBaseId = question.Id,
                QuestionBase = question,
                CurrentPoints = 600,
                CurrentMinusPoints = 165,
            };

            game.GameGridCoordinates.Add(coordinate);

            return new CurrentQuestionViewModel { Coordinate = coordinate };
        }

        /// <summary>
        /// Das Spielleiter-Fenster spricht Deutsch, und die Knoepfe sagen, was sie bewirken.
        /// </summary>
        [TestMethod]
        public void TheMasterWindowSpeaksGerman()
        {
            OnUiThread(() =>
            {
                var fenster = new GameMasterView();
                var inhalt = Layout(fenster);

                var texte = VisibleTexts(inhalt);

                Assert.IsTrue(texte.Count > 0, "Es ist gar nichts sichtbar angekommen.");

                foreach (var erwartet in new[]
                         {
                             "Spielstand", "Wer wählt aus", "Phase", "Bei den Spielern",
                             "Spieleransicht öffnen", "Buzzer-Server öffnen",
                         })
                {
                    Assert.IsTrue(texte.Any(t => t.Contains(erwartet)),
                        $"\"{erwartet}\" fehlt im Spielleiter-Fenster. Sichtbar ist: "
                        + string.Join(" | ", texte));
                }

                foreach (var englisch in new[]
                         {
                             "Buzzer Server", "Toggle Fullscreen", "Open Player View",
                             "Change View", "Show Stats", "Round: ", "Done: ",
                         })
                {
                    Assert.IsFalse(texte.Any(t => t == englisch),
                        $"Die englische Beschriftung \"{englisch}\" steht noch da.");
                }
            });
        }

        /// <summary>
        /// Keine Bindung darf ins Leere greifen.
        /// <para>
        /// Eine Bindung auf eine Eigenschaft, die es nicht gibt, wirft nicht - sie bleibt still
        /// auf dem Standardwert stehen. Bis 2026-09-06 hing so die Sichtbarkeit des Spielfelds
        /// an <c>ShowGameGridView</c>, das es auf diesem ViewModel gar nicht gibt. Es fiel nur
        /// deshalb nicht auf, weil der Standardwert zufaellig der richtige war.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheMasterWindowHasNoDeadBindings()
        {
            OnUiThread(() =>
            {
                using var wache = new BindingErrorWatch();

                var fenster = new GameMasterView();
                Layout(fenster);

                Assert.AreEqual(0, wache.Errors.Count,
                    "Im Spielleiter-Fenster greifen Bindungen ins Leere: "
                    + string.Join(" || ", wache.Errors));
            });
        }

        /// <summary>Dasselbe fuer das Fragefenster.</summary>
        [TestMethod]
        public void TheQuestionWindowHasNoDeadBindings()
        {
            OnUiThread(() =>
            {
                using var wache = new BindingErrorWatch();

                var vm = BuildQuestionViewModel();
                var fenster = new QuestionMasterView { DataContext = vm };
                Layout(fenster, 1300, 800);

                Assert.AreEqual(0, wache.Errors.Count,
                    "Im Fragefenster greifen Bindungen ins Leere: "
                    + string.Join(" || ", wache.Errors));
            });
        }

        /// <summary>
        /// Das Fragefenster nennt Kategorie, Stufe, Punkte und Phase in einer Zeile - statt in
        /// neun schreibgeschuetzten Textfeldern, die ein Viertel des Fensters belegten.
        /// </summary>
        [TestMethod]
        public void TheQuestionWindowSumsUpTheQuestionInOneLine()
        {
            OnUiThread(() =>
            {
                var vm = BuildQuestionViewModel();
                var fenster = new QuestionMasterView { DataContext = vm };
                var inhalt = Layout(fenster, 1300, 800);

                var texte = VisibleTexts(inhalt);

                var zusammenfassung = texte.FirstOrDefault(t => t.Contains("Punkte") && t.Contains("Phase"));

                Assert.IsNotNull(zusammenfassung,
                    "Die Zeile mit Punkten und Phase fehlt. Sichtbar ist: " + string.Join(" | ", texte));

                StringAssert.Contains(zusammenfassung!, "600");
                StringAssert.Contains(zusammenfassung!, "Phase 2");

                Assert.IsTrue(texte.Any(t => t.Contains("Jetzt sichtbar")),
                    "Die Kennzeichnung des laufenden Schrittes fehlt.");

                Assert.IsFalse(texte.Any(t => t is "Current Step" or "Next Step" or "Finish Step"),
                    "Die englischen Ueberschriften stehen noch da.");
            });
        }

        /// <summary>Die Knoepfe zum Abschliessen sagen, was sie tun.</summary>
        [TestMethod]
        public void TheQuestionWindowButtonsSayWhatTheyDo()
        {
            OnUiThread(() =>
            {
                var vm = BuildQuestionViewModel();
                var fenster = new QuestionMasterView { DataContext = vm };
                var inhalt = Layout(fenster, 1300, 800);

                var texte = VisibleTexts(inhalt);

                foreach (var erwartet in new[]
                         {
                             "Bewerten …", "Zelle abschließen", "Schließen, nächster wählt",
                             "Schließen ohne Wechsel", "Noch nicht abgeschlossen",
                         })
                {
                    Assert.IsTrue(texte.Any(t => t.Contains(erwartet)),
                        $"\"{erwartet}\" fehlt. Sichtbar ist: " + string.Join(" | ", texte));
                }

                Assert.IsFalse(texte.Any(t => t is "Results" or "Save/IsDone/Finish" or "Exit/Next Ch. P." or "Exit"),
                    "Eine englische Knopfbeschriftung steht noch da.");
            });
        }
    }
}
