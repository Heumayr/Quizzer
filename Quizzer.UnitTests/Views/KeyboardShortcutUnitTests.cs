using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Die Tastenkürzel des Fragefensters: Enter schaltet weiter, die Rücktaste zurück.
    /// <para>
    /// <b>Gemessen 2026-09-06:</b> sie wirkten nur, solange der Spielleiter noch nichts
    /// angeklickt hatte. Danach hielt ein Knopf den Fokus und verschluckte die Taste - die
    /// <c>InputBindings</c> am Fenster greifen erst, wenn das Ereignis bis dorthin hochblubbert.
    /// Im Spielabend heißt das: nach dem ersten Mausklick ist das Kürzel weg, und genau das
    /// wurde gemeldet.
    /// </para>
    /// </summary>
    [TestClass]
    public class KeyboardShortcutUnitTests
    {
        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 3, playerCount: 2);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

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

        /// <summary>
        /// Ahmt nach, was Windows tut: erst das tunnelnde <c>PreviewKeyDown</c>, dann - falls
        /// niemand es behandelt hat - das blubbernde <c>KeyDown</c>. Nur die erste Hälfte
        /// erreicht ein Fenster, dessen Bedienelemente den Fokus haben; genau deshalb hängt der
        /// Riegel dort.
        /// </summary>
        private static bool Druecke(Window fenster, Key taste)
        {
            var ui = (Keyboard.FocusedElement as UIElement) ?? fenster;
            var quelle = PresentationSource.FromVisual(fenster);

            var vorschau = new KeyEventArgs(Keyboard.PrimaryDevice, quelle, 0, taste)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };

            ui.RaiseEvent(vorschau);

            if (vorschau.Handled)
                return true;

            var blubbernd = new KeyEventArgs(Keyboard.PrimaryDevice, quelle, 0, taste)
            {
                RoutedEvent = Keyboard.KeyDownEvent,
            };

            ui.RaiseEvent(blubbernd);

            return blubbernd.Handled;
        }

        /// <summary>Öffnet das Fragefenster weit außerhalb des Bildschirms und legt es aus.</summary>
        private static QuestionMasterView Oeffne(CurrentQuestionViewModel vm)
        {
            var fenster = new QuestionMasterView
            {
                DataContext = vm,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -3000,
                Top = -3000,
            };

            fenster.Show();
            fenster.UpdateLayout();

            return fenster;
        }

        /// <summary>
        /// Der gemeldete Fall: nach einem Mausklick hält ein Knopf den Fokus, und Enter muss
        /// trotzdem weiterschalten.
        /// </summary>
        [TestMethod]
        public async Task EnterStillAdvancesAfterAButtonWasClicked()
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            UiTestHost.Run(() =>
            {
                var fenster = Oeffne(vm);

                try
                {
                    Druecke(fenster, Key.Enter);

                    var ersterSchritt = vm.CurrentStep;

                    Assert.IsNotNull(ersterSchritt,
                        "Ohne vorherigen Klick wirkte Enter nicht - dann misst der Rest nichts.");

                    // Jetzt so, wie der Spielleiter es tut: einen Knopf anklicken.
                    var knopf = Descendants<Button>(fenster).First(b => b.IsEnabled && b.IsVisible);

                    knopf.Focus();

                    Assert.AreSame(knopf, Keyboard.FocusedElement,
                        "Der Knopf hat den Fokus nicht bekommen - dann misst der Test nichts.");

                    Druecke(fenster, Key.Enter);

                    Assert.AreNotSame(ersterSchritt, vm.CurrentStep,
                        "Enter wirkte nicht mehr, weil der Knopf die Taste verschluckt hat. "
                        + "Genau das war der gemeldete Fehler.");
                }
                finally
                {
                    fenster.Close();
                }
            });
        }

        /// <summary>
        /// Die Rücktaste geht denselben Weg zurück.
        /// </summary>
        [TestMethod]
        public async Task BackspaceStepsBackEvenWithAFocusedButton()
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            UiTestHost.Run(() =>
            {
                var fenster = Oeffne(vm);

                try
                {
                    Druecke(fenster, Key.Enter);
                    Druecke(fenster, Key.Enter);

                    var zweiterSchritt = vm.CurrentStep;

                    Assert.IsNotNull(zweiterSchritt);

                    Descendants<Button>(fenster).First(b => b.IsEnabled && b.IsVisible).Focus();

                    Druecke(fenster, Key.Back);

                    Assert.AreNotSame(zweiterSchritt, vm.CurrentStep,
                        "Die Ruecktaste kam bei fokussiertem Knopf nicht durch.");
                }
                finally
                {
                    fenster.Close();
                }
            });
        }

        /// <summary>
        /// Die Gegenrichtung, und sie ist die wichtigere: in einem mehrzeiligen Textfeld gehört
        /// Enter dem Text. Ohne diese Zusicherung wäre der Riegel oben ein Fehler, der das
        /// Schreiben unmöglich macht - und das fiele erst im Frage-Editor auf.
        /// </summary>
        [TestMethod]
        public async Task InAMultilineTextBoxEnterStillBelongsToTheText()
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            UiTestHost.Run(() =>
            {
                var fenster = Oeffne(vm);

                try
                {
                    Druecke(fenster, Key.Enter);

                    var schritt = vm.CurrentStep;

                    // Ein mehrzeiliges Feld, wie es der Frage-Editor verwendet.
                    var feld = new TextBox { AcceptsReturn = true };

                    var wurzel = (Panel)((FrameworkElement)fenster.Content).FindName("Wurzel")
                                 ?? Descendants<Panel>(fenster).First();

                    wurzel.Children.Add(feld);
                    fenster.UpdateLayout();

                    feld.Focus();

                    Assert.AreSame(feld, Keyboard.FocusedElement,
                        "Das Textfeld hat den Fokus nicht bekommen - dann misst der Test nichts.");

                    Druecke(fenster, Key.Enter);

                    Assert.AreSame(schritt, vm.CurrentStep,
                        "Enter im Textfeld hat weitergeschaltet - im Editor liesse sich damit "
                        + "keine zweite Zeile mehr schreiben.");
                }
                finally
                {
                    fenster.Close();
                }
            });
        }
    }
}
