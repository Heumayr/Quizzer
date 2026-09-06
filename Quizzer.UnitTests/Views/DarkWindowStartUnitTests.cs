using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Kein Fenster startet hell.
    /// <para>
    /// <b>Gemeldet 2026-09-06:</b> „beim wechsel der fenster immer das ganze fenster weiß ... da
    /// eigentlich der darkmode ... also alles eher dunkel ist ... tut dies sehr in den augen
    /// weh". Die Ursache ist der Standard von WPF: <c>Window.Background</c> ist
    /// <c>SystemColors.WindowBrush</c>, und das ist weiß. Der Stil aus <c>App.xaml</c> setzt zwar
    /// einen dunklen Grund, greift aber erst beim ersten Auslegen - der weiße Grund steht vorher
    /// schon auf dem Bildschirm.
    /// </para>
    /// <para>
    /// Deshalb setzt <c>WindowBase</c> den Grund örtlich im Konstruktor. Dieser Test misst genau
    /// das: <b>vor</b> jedem Auslegen, direkt nach dem Erzeugen.
    /// </para>
    /// </summary>
    [TestClass]
    public class DarkWindowStartUnitTests
    {
        /// <summary>Ab hier gilt eine Farbe als dunkel: Summe der Kanäle unter 300 von 765.</summary>
        private const int DunkelGrenze = 300;

        private static readonly Type[] Fenster =
        [
            typeof(Quizzer.MainWindow),
            typeof(Quizzer.Views.CategoriesView),
            typeof(Quizzer.Views.EditGameView),
            typeof(Quizzer.Views.EditPlayerView),
            typeof(Quizzer.Views.GamesView),
            typeof(Quizzer.Views.GameViews.GameMasterView),
            typeof(Quizzer.Views.GameViews.GamePlayerView),
            typeof(Quizzer.Views.GameViews.PlayersResultView),
            typeof(Quizzer.Views.GameViews.QuestionMasterView),
            typeof(Quizzer.Views.HelperViewModels.QuestionSelectorView),
            typeof(Quizzer.Views.PlayersView),
            typeof(Quizzer.Views.QuestionsView),
            typeof(Quizzer.Views.QuestionTypes.EditQuestionsView),
            typeof(Quizzer.Views.QuestionTypes.NewQuestionView),
            typeof(Quizzer.Views.BuzzerViews.BuzzerServerView),
            typeof(Quizzer.Views.GameThemesView),
            typeof(Quizzer.Views.LoginView),
            typeof(Quizzer.Views.SettingsView),
            typeof(Quizzer.Views.ImportPlayersView),
            typeof(Quizzer.Views.QuestionTypes.QuestionPreviewView),
            typeof(Quizzer.Views.QuestionTypes.RevealEditorView),
        ];

        private static int Helligkeit(Brush? pinsel)
            => pinsel is SolidColorBrush s ? s.Color.R + s.Color.G + s.Color.B : -1;

        /// <summary>
        /// Jedes Fenster ist schon beim Erzeugen dunkel - noch bevor irgendetwas ausgelegt oder
        /// gezeichnet wurde.
        /// </summary>
        [TestMethod]
        public void EveryWindowIsDarkFromTheMomentItIsCreated()
        {
            var funde = new List<string>();
            var geprueft = 0;

            UiTestHost.Run(() =>
            {
                foreach (var typ in Fenster)
                {
                    // Siehe FensterAufraeumer: ohne Schliessen bleibt jedes erzeugte Fenster
                    // fuer den ganzen Testlauf in Application.Windows stehen.
                    using var wegraeumen = new FensterAufraeumer(typ);

                    var fenster = wegraeumen.Fenster;

                    geprueft++;

                    var helligkeit = Helligkeit(fenster.Background);

                    if (helligkeit < 0)
                    {
                        funde.Add($"{typ.Name}: kein einfarbiger Grund ({fenster.Background?.GetType().Name ?? "null"})");
                        continue;
                    }

                    if (helligkeit >= DunkelGrenze)
                        funde.Add($"{typ.Name}: Helligkeit {helligkeit} - startet hell");
                }
            });

            Assert.AreEqual(Fenster.Length, geprueft,
                $"Nur {geprueft} von {Fenster.Length} Fenstern erzeugt.");

            Assert.AreEqual(0, funde.Count,
                "Diese Fenster blitzen beim Oeffnen hell auf:" + Environment.NewLine
                + string.Join(Environment.NewLine, funde));
        }

        /// <summary>
        /// Die Gegenrichtung: der Vergleich muss hell auch als hell erkennen. Ohne sie wäre die
        /// Zusicherung oben auch dann grün, wenn <c>Helligkeit</c> immer 0 lieferte.
        /// </summary>
        [TestMethod]
        public void TheBrightnessCheckActuallyRecognisesBright()
        {
            Assert.IsTrue(Helligkeit(Brushes.White) >= DunkelGrenze,
                "Weiss wird nicht als hell erkannt - dann misst der Test nichts.");

            Assert.IsTrue(Helligkeit(SystemColors.WindowBrush) >= DunkelGrenze,
                "Der WPF-Standardgrund wird nicht als hell erkannt - und genau der war das "
                + "Problem.");

            Assert.IsTrue(Helligkeit(Quizzer.Base.WindowBase.DunklerGrund) < DunkelGrenze,
                "Der eigene Grund gilt nicht als dunkel.");
        }
    }
}
