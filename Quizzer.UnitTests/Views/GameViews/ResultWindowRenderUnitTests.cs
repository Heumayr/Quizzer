using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.DataModels.Enumerations;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Das Fenster, in dem der Spielleiter die Punkte vergibt - mit so vielen Mitspielern, wie
    /// ein Quizabend sie hat.
    /// <para>
    /// Es verteilte die Karten auf so viele Spalten, wie es Spieler gibt: bei sechs Gaesten
    /// blieben je Karte rund 133 Bildpunkte, waehrend das Spielerbild allein 150 breit ist. Die
    /// Karten ueberzeichneten einander, und die Knopfleiste konnte aus dem Fenster rutschen.
    /// </para>
    /// </summary>
    [TestClass]
    public class ResultWindowRenderUnitTests
    {
        private const double FensterBreite = 1100;
        private const double FensterHoehe = 700;

        private TestGameBuilder world = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 1, playerCount: 6);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

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

        /// <summary>Baut das Fenster mit einer geladenen Zelle und legt es aus.</summary>
        private async Task<PlayersResultViewModel> BuildViewModelAsync()
        {
            var frage = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await frage.LoadForTestAsync();

            Assert.IsNotNull(frage.PlayersResultViewModel, "Die Ergebnisansicht wurde nicht gebaut.");

            return frage.PlayersResultViewModel!;
        }

        /// <summary>
        /// Sechs Mitspieler: die Karten duerfen einander nicht ueberzeichnen. Vier je Zeile,
        /// der Rest bricht um.
        /// </summary>
        [TestMethod]
        public async Task WithSixPlayersTheCardsDoNotOverlap()
        {
            var vm = await BuildViewModelAsync();

            OnUiThread(() =>
            {
                var fenster = new PlayersResultView { DataContext = vm };
                var inhalt = (FrameworkElement)fenster.Content;

                inhalt.Measure(new Size(FensterBreite, FensterHoehe));
                inhalt.Arrange(new Rect(0, 0, FensterBreite, FensterHoehe));
                inhalt.UpdateLayout();

                Assert.AreEqual(4, vm.Columns,
                    "Bei sechs Spielern duerfen hoechstens vier Karten nebeneinander stehen.");

                var karten = Descendants<UcPlayerResultView>(inhalt).ToList();

                Assert.AreEqual(6, karten.Count, "Es fehlt eine Spielerkarte.");

                foreach (var karte in karten)
                {
                    Assert.IsTrue(karte.ActualWidth >= 150,
                        $"Eine Karte ist nur {karte.ActualWidth:0} Bildpunkte breit - das "
                        + "Spielerbild allein braucht 150.");
                }
            });
        }

        /// <summary>
        /// Die Knopfleiste muss sichtbar bleiben, egal wie viele Karten es gibt - sie ist der
        /// einzige Weg, die Punkte zu schreiben.
        /// </summary>
        [TestMethod]
        public async Task TheSaveButtonsStayVisible()
        {
            var vm = await BuildViewModelAsync();

            OnUiThread(() =>
            {
                var fenster = new PlayersResultView { DataContext = vm };
                var inhalt = (FrameworkElement)fenster.Content;

                inhalt.Measure(new Size(FensterBreite, FensterHoehe));
                inhalt.Arrange(new Rect(0, 0, FensterBreite, FensterHoehe));
                inhalt.UpdateLayout();

                var knoepfe = Descendants<Button>(inhalt)
                    .Where(b => b.Content is string text && text.StartsWith("Speichern"))
                    .ToList();

                Assert.AreEqual(3, knoepfe.Count,
                    "Die drei Speicherknoepfe sind nicht alle da.");

                foreach (var knopf in knoepfe)
                {
                    var position = knopf.TranslatePoint(new Point(0, 0), inhalt);

                    Assert.IsTrue(position.Y + knopf.ActualHeight <= FensterHoehe + 1,
                        $"Der Knopf \"{knopf.Content}\" sitzt bei {position.Y:0} und ragt aus "
                        + "dem Fenster - der Spielleiter kaeme nicht mehr an ihn heran.");
                }
            });
        }

        /// <summary>Die Karte sagt im Klartext, welche Bewertung gesetzt ist.</summary>
        [TestMethod]
        public async Task TheCardSpellsOutTheRating()
        {
            var vm = await BuildViewModelAsync();

            OnUiThread(() =>
            {
                var fenster = new PlayersResultView { DataContext = vm };
                var inhalt = (FrameworkElement)fenster.Content;

                inhalt.Measure(new Size(FensterBreite, FensterHoehe));
                inhalt.Arrange(new Rect(0, 0, FensterBreite, FensterHoehe));
                inhalt.UpdateLayout();

                var texte = Descendants<TextBlock>(inhalt).Select(t => t.Text).ToList();

                Assert.IsTrue(texte.Any(t => t.Contains("Noch nicht bewertet")),
                    "Ohne Bewertung muss das dastehen. Sichtbar ist: " + string.Join(" | ", texte.Take(20)));

                var ersteKarte = vm.PlayerResultContextList[0];
                ersteKarte.Suggestion = PlayerResultContext.ScoreSuggestion.Wrong;

                inhalt.UpdateLayout();

                var neueTexte = Descendants<TextBlock>(inhalt).Select(t => t.Text).ToList();

                Assert.IsTrue(neueTexte.Any(t => t.StartsWith("Bewertung: falsch")),
                    "Nach dem Setzen fehlt die Klartextzeile. Sichtbar ist: "
                    + string.Join(" | ", neueTexte.Take(20)));
            });
        }
    }
}
