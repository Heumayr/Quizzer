using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Kein Text der Anwendung ist unsichtbar - über alle Fenster gemessen.
    /// <para>
    /// <b>Nutzermeldung vom 2026-09-06 nachts:</b> „text in schwarz ist nich sichtbar". Vier
    /// Bereichsüberschriften im Frageneditor standen mit 1,1:1 auf dem dunklen Grund. Die
    /// Ursache war kein Tippfehler, sondern eine WPF-Regel: <b>ein ausdrücklich gesetzter Stil
    /// ersetzt den impliziten vollständig</b>, und ohne <c>BasedOn</c> fällt die Schriftfarbe auf
    /// den Standard Schwarz zurück.
    /// </para>
    /// <para>
    /// <b>Gemessen wird das gezeichnete Bild, nicht die Farbeigenschaft.</b> Ein Kontrastmaß aus
    /// <c>Foreground</c> gegen die nächste gesetzte <c>Background</c> rät bei CheckBox und
    /// RadioButton falsch - deren <c>Background</c> färbt in der Standardvorlage nur das
    /// Kästchen, nicht die Fläche hinter der Beschriftung. Es meldete darum sechs Stellen, an
    /// denen in Wahrheit nichts fehlte.
    /// </para>
    /// <para>
    /// <b>Zwei Fallen stecken in der Messung selbst</b>, beide teuer bezahlt: <c>IsVisible</c> ist
    /// auf einem nie gezeigten Fenster immer <c>false</c> - damit meldete die Probe gar nichts.
    /// Und <c>RenderTargetBitmap.Render</c> zeichnet das Wurzelelement an <i>seinem</i>
    /// Layout-Versatz, während die Verwandlung Punkte relativ zu dessen Ursprung liefert; ohne
    /// Ausgleich misst man neben dem Text, und in <c>LoginView</c> galt alles als unsichtbar.
    /// </para>
    /// </summary>
    [TestClass]
    public class LesbarkeitUnitTests
    {
        /// <summary>Unter diesem Verhältnis gilt ein Text als nicht erkennbar.</summary>
        private const double Mindestspanne = 3.0;
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
        /// Ob das Element und jeder seiner Vorfahren sichtbar geschaltet ist.
        /// <para>
        /// <b>Nicht <c>IsVisible</c>:</b> das ist auf einem nie gezeigten Fenster immer
        /// <c>false</c>, und die Probe meldete damit gar nichts. Ein eingeklappter Bereich muss
        /// trotzdem übersprungen werden - sonst misst man Text, den niemand sieht, weil er
        /// nirgends steht.
        /// </para>
        /// </summary>
        private static bool Sichtbar(DependencyObject? knoten)
        {
            while (knoten != null)
            {
                if (knoten is UIElement e && e.Visibility != Visibility.Visible)
                    return false;

                knoten = VisualTreeHelper.GetParent(knoten);
            }

            return true;
        }

        private static double Helligkeit(byte r, byte g, byte b)
        {
            static double K(double v)
            {
                v /= 255.0;
                return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * K(r) + 0.7152 * K(g) + 0.0722 * K(b);
        }

        /// <summary>
        /// Wie stark sich der hellste und der dunkelste Punkt im Rechteck unterscheiden - als
        /// Kontrastverhältnis. Sichtbarer Text erzeugt immer einen Unterschied; ein Rechteck ohne
        /// jeden Unterschied enthält nichts Sichtbares.
        /// </summary>
        private static double Spanne(byte[] bild, int breite, int hoehe, Int32Rect r)
        {
            var hell = 0.0;
            var dunkel = 1.0;

            for (var y = r.Y; y < r.Y + r.Height && y < hoehe; y++)
            {
                for (var x = r.X; x < r.X + r.Width && x < breite; x++)
                {
                    var i = (y * breite + x) * 4;
                    var l = Helligkeit(bild[i + 2], bild[i + 1], bild[i]);

                    if (l > hell) hell = l;
                    if (l < dunkel) dunkel = l;
                }
            }

            return (hell + 0.05) / (dunkel + 0.05);
        }

        /// <summary>
        /// In jedem Fenster hebt sich jeder sichtbare Text von seinem Grund ab.
        /// </summary>
        [TestMethod]
        public void NoTextIsInvisibleInAnyWindow()
        {
            var funde = Suche(Mindestspanne);

            Assert.AreEqual(0, funde.Count,
                "Diese Texte sind auf ihrem Grund nicht zu erkennen:" + Environment.NewLine
                + string.Join(Environment.NewLine, funde));
        }

        /// <summary>
        /// Das Auffangnetz unter allem: <b>jedes Fenster startet mit heller Schrift.</b>
        /// <para>
        /// <b>Gemessen am 2026-09-06:</b> der Vordergrund jedes Fensters war <c>#FF000000</c> aus
        /// <c>DefaultStyle</c>. Der Stil <c>&lt;Style TargetType="{x:Type base:WindowBase}"&gt;</c>
        /// greift nie - ein impliziter Stil bindet in WPF auf den <b>genauen</b> Typ, und kein
        /// Fenster <i>ist</i> ein <c>WindowBase</c>, alle leiten davon ab.
        /// </para>
        /// <para>
        /// Sichtbar war das nirgends, weil jedes Textelement seine Farbe vom impliziten
        /// TextBlock-Stil bekommt. Genau deshalb steht es hier: es ist die Ebene, die trägt, wenn
        /// darüber einmal etwas durchfällt.
        /// </para>
        /// </summary>
        [TestMethod]
        public void EveryWindowStartsWithLightText()
        {
            var dunkel = new List<string>();

            UiTestHost.Run(() =>
            {
                foreach (var typ in Fenster)
                {
                    using var wegraeumen = new FensterAufraeumer(typ);

                    var vg = (wegraeumen.Fenster.Foreground as SolidColorBrush)?.Color;
                    var bg = (wegraeumen.Fenster.Background as SolidColorBrush)?.Color;

                    if (vg == null || bg == null)
                    {
                        dunkel.Add($"{typ.Name}: vg={vg} bg={bg}");
                        continue;
                    }

                    var k = (Helligkeit(vg.Value.R, vg.Value.G, vg.Value.B) + 0.05)
                            / (Helligkeit(bg.Value.R, bg.Value.G, bg.Value.B) + 0.05);

                    if (k < Mindestspanne)
                        dunkel.Add($"{typ.Name}: {k:0.0}:1 vg={vg} bg={bg}");
                }
            });

            Assert.AreEqual(0, dunkel.Count,
                "Diese Fenster starten mit einer Schriftfarbe, die sich vom eigenen Grund nicht "
                + "abhebt - faellt darueber ein Stil durch, ist der Text weg:" + Environment.NewLine
                + string.Join(Environment.NewLine, dunkel));
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ohne sie prüfte die Zusicherung oben nur eine Abwesenheit:
        /// misst die Probe gar nichts - falsche Bildgröße, falscher Versatz, kein Text gefunden -,
        /// bliebe sie ebenso grün. Bei einer absurd hohen Schwelle muss <b>jeder</b> Text
        /// auffallen.
        /// </summary>
        [TestMethod]
        public void TheProbeWouldNoticeIfItMeasuredNothing()
        {
            var funde = Suche(1000.0);

            Assert.IsTrue(funde.Count > 50,
                "Bei einer Schwelle von 1000:1 muesste praktisch jeder Text auffallen. Gefunden "
                + $"wurden nur {funde.Count} - die Probe misst also nicht, was sie zu messen "
                + "vorgibt.");
        }

        private static List<string> Suche(double schwelle)
        {
            var funde = new List<string>();

            UiTestHost.Run(() =>
            {
                foreach (var typ in Fenster)
                {
                    using var wegraeumen = new FensterAufraeumer(typ);

                    var inhalt = (FrameworkElement)wegraeumen.Fenster.Content;

                    inhalt.Measure(new Size(1400, 900));
                    inhalt.Arrange(new Rect(0, 0, 1400, 900));
                    inhalt.UpdateLayout();

                    var versatz = VisualTreeHelper.GetOffset(inhalt);

                    // Der Versatz auf BEIDEN Seiten dazu: sonst schneidet der Bildrand die
                    // unterste Zeile ab, und abgeschnitten sieht aus wie unsichtbar. In
                    // LoginView lagen "Beenden" und "Anmelden" genau so daneben.
                    var breite = (int)Math.Ceiling(inhalt.ActualWidth + versatz.X * 2) + 2;
                    var hoehe = (int)Math.Ceiling(inhalt.ActualHeight + versatz.Y * 2) + 2;

                    if (breite <= 0 || hoehe <= 0)
                        continue;

                    var ziel = new RenderTargetBitmap(breite, hoehe, 96, 96, PixelFormats.Pbgra32);

                    ziel.Render(inhalt);

                    var schritt = breite * 4;
                    var bild = new byte[schritt * hoehe];

                    ziel.CopyPixels(bild, schritt, 0);

                    foreach (var knoten in Alle(inhalt))
                    {
                        if (knoten is not TextBlock t || string.IsNullOrWhiteSpace(t.Text))
                            continue;

                        if (t.ActualWidth < 1 || t.ActualHeight < 1 || !Sichtbar(t))
                            continue;

                        var oben = t.TransformToAncestor(inhalt).Transform(new Point(0, 0));

                        // Render zeichnet das Wurzelelement an SEINEM Layout-Versatz, die
                        // Verwandlung liefert Punkte relativ zu dessen Ursprung. Ohne diese
                        // Verschiebung misst man neben dem Text - LoginView liegt 28 Punkte
                        // daneben und meldete alles als unsichtbar.
                        var r = new Int32Rect(
                            Math.Max(0, (int)(oben.X + versatz.X)),
                            Math.Max(0, (int)(oben.Y + versatz.Y)),
                            (int)Math.Ceiling(t.ActualWidth),
                            (int)Math.Ceiling(t.ActualHeight));

                        if (r.X >= breite || r.Y >= hoehe)
                            continue;

                        var s = Spanne(bild, breite, hoehe, r);

                        if (s < schwelle)
                        {
                            funde.Add($"{typ.Name} {s:0.0}:1 "
                                + $"[{t.Text[..Math.Min(55, t.Text.Length)]}]");
                        }
                    }
                }
            });

            return funde;
        }
    }
}
