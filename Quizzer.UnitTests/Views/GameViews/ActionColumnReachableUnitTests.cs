using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.BuzzerViews;
using Quizzer.Views.GameViews;
using Quizzer.Views.StaticRessources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Die Handlungsknoepfe rechts unten im Fragefenster bleiben erreichbar - auch bei voller
    /// Mannschaft.
    /// <para>
    /// <b>Gemessen am 2026-09-06</b>, bei 1300x800: mit zwei Mitspielern endet der unterste Knopf
    /// bei 705, mit sechs bei 777, mit zehn bei 849 - also 49 Bildpunkte ausserhalb des Fensters.
    /// Zwei der vier Knoepfe sind dann weg. Ursache ist die Spielerliste darueber: sie steht in
    /// einer Auto-Zeile und schiebt die Knopfspalte je Mitspieler um rund 18 Punkte nach unten.
    /// </para>
    /// <para>
    /// <b>Warum das eine eigene Zusicherung braucht:</b> <c>NoWindowOverflowsItsOwnWidth</c> kann
    /// es nie finden. Es baut das Fragefenster mit dem im XAML erklaerten ViewModel, ganz ohne
    /// Zelle - die Spielerliste ist dann leer und nimmt kaum Platz.
    /// </para>
    /// <para>
    /// Und ein Knopf, der aus dem Fenster gerutscht ist, meldet sich nicht: er ist einfach nicht
    /// mehr da. Der Spielleiter merkt es erst, wenn er ihn braucht - vor Gaesten.
    /// </para>
    /// </summary>
    [TestClass]
    public class ActionColumnReachableUnitTests
    {
        private const double Breite = 1300;

        /// <summary>Ein kleines, aber gebraeuchliches Fenster - der Boden, nicht der Regelfall.</summary>
        private const double Hoehe = 800;

        /// <summary>
        /// Die Knoepfe, ohne die eine Zelle nicht zu Ende gespielt werden kann - samt
        /// "Runde zuruecksetzen", das seit dem 2026-09-06 ebenfalls in dieser Spalte steht
        /// (Nutzerentscheidung F10).
        /// </summary>
        private static readonly string[] Pflichtknoepfe =
        [
            "Bewerten", "Abschließen", "Nächster wählt aus", "Gleicher wählt weiter",
            "Runde zurücksetzen",
        ];

        private TestGameBuilder? world;

        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            if (world != null)
                await world.DisposeAsync();

            UserPrompt.Reset();
            StaticManager.Reset();
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

        /// <summary>Was eine Messung ergeben hat.</summary>
        private sealed record Messung(List<string> Funde, List<string> Gefunden, int Mannschaft);

        /// <summary>
        /// Legt ein Spiel mit <paramref name="spieler"/> Mitspielern an, oeffnet das Fragefenster
        /// und meldet jeden Pflichtknopf, der ausserhalb des Fensters landet.
        /// </summary>
        private async Task<Messung> MessenAsync(int spieler, double hoehe)
        {
            world = await TestGameBuilder.CreateAsync(QuestionType.Default, normalStepCount: 3, playerCount: spieler);

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();

            // Die Spielerliste kommt aus dem prozessweiten BuzzerServerViewModel und fuellt sich
            // sonst erst, wenn der Server laeuft. Ohne sie stuende die Knopfspalte weit oben, und
            // die Messung saehe den vollen Quizabend gar nicht.
            var server = StaticManager.BuzzerServerViewModel;

            server.Game = world.Game;
            server.BuzzerControlsViewModel = new BuzzerControlsViewModel();
            server.BuzzerControlsViewModel.SetBuzzerVerverViewModel(server);
            server.BuzzerControlsViewModel.SetRoundInfo(world.Question);

            var funde = new List<string>();
            var gefunden = new List<string>();

            UiTestHost.Run(() =>
            {
                var fenster = new QuestionMasterView { DataContext = vm };
                var inhalt = (FrameworkElement)fenster.Content;

                inhalt.Measure(new Size(Breite, hoehe));
                inhalt.Arrange(new Rect(0, 0, Breite, hoehe));
                inhalt.UpdateLayout();

                foreach (var knopf in Descendants<Button>(inhalt))
                {
                    var beschriftung = knopf.Content?.ToString() ?? string.Empty;

                    var passt = Pflichtknoepfe.FirstOrDefault(
                        p => beschriftung.Contains(p, StringComparison.OrdinalIgnoreCase));

                    if (passt == null)
                        continue;

                    gefunden.Add(passt);

                    var oben = knopf.TranslatePoint(new Point(0, 0), inhalt);
                    var unten = oben.Y + knopf.ActualHeight;

                    if (unten > hoehe + 1)
                        funde.Add($"\"{beschriftung}\" endet bei {unten:0} - das Fenster ist {hoehe:0} hoch");

                    if (oben.X + knopf.ActualWidth > Breite + 1)
                        funde.Add($"\"{beschriftung}\" endet bei x={oben.X + knopf.ActualWidth:0}");

                    if (knopf.ActualHeight <= 0 || knopf.ActualWidth <= 0)
                        funde.Add($"\"{beschriftung}\" wurde gar nicht ausgelegt");
                }
            });

            return new Messung(funde, gefunden, server.BuzzerControlsViewModel.LivePlayers.Count);
        }

        /// <summary>
        /// Jeder Pflichtknopf liegt vollstaendig innerhalb des Fensters - bei zwei Mitspielern
        /// ebenso wie bei zehn.
        /// </summary>
        [TestMethod]
        [DataRow(2)]
        [DataRow(6)]
        [DataRow(10)]
        public async Task NoActionButtonLeavesTheWindow(int spieler)
        {
            var messung = await MessenAsync(spieler, Hoehe);

            // Ohne diese Zusicherung waere der Test auch dann gruen, wenn die Knoepfe gar nicht im
            // Baum sind - dann faende die Schleife nichts und meldete nichts.
            foreach (var pflicht in Pflichtknoepfe)
            {
                Assert.IsTrue(messung.Gefunden.Contains(pflicht),
                    $"Der Knopf \"{pflicht}\" wurde im Fenster gar nicht gefunden.");
            }

            // Und die zweite Falle: die Spielerliste ist es, die die Knoepfe nach unten schiebt.
            // Ist sie leer, misst der Test die Mannschaftsgroesse gar nicht.
            Assert.AreEqual(spieler, messung.Mannschaft,
                "Die Spielerliste im Fragefenster ist nicht gefuellt. Dann steht die Knopfspalte "
                + "weit oben, und diese Messung sagt ueber den vollen Quizabend nichts aus.");

            Assert.AreEqual(0, messung.Funde.Count,
                $"Mit {spieler} Mitspielern sind diese Knoepfe nicht mehr erreichbar:"
                + Environment.NewLine + string.Join(Environment.NewLine, messung.Funde));
        }

        /// <summary>
        /// Die Gegenrichtung: die Messung muss einen Knopf ausserhalb des Fensters auch wirklich
        /// bemerken. Ohne diese Probe waere oben auch dann alles gruen, wenn der Vergleich nie
        /// zuschlaegt - etwa weil ein Elternteil die Knoepfe abschneidet, statt sie zu verschieben.
        /// </summary>
        [TestMethod]
        public async Task TheMeasurementActuallyNoticesAButtonOutsideTheWindow()
        {
            var messung = await MessenAsync(spieler: 6, hoehe: 420);

            Assert.AreNotEqual(0, messung.Funde.Count,
                "In einem 420 Punkte hohen Fenster passen die vier Knoepfe unmoeglich alle hinein. "
                + "Meldet die Messung hier nichts, misst sie ueberhaupt nichts.");
        }
    }
}
