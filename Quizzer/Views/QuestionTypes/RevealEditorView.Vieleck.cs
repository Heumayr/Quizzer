using Quizzer.DataModels.Questions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Das Vieleck: Punkte setzen, die sich zu einer Fläche verbinden (Nutzerwunsch 2026-10-21:
    /// „ein freihandtool wo man punkte setzen kann die sich zu polygonen verbinden").
    /// <para>
    /// <b>Bedienung:</b> jeder Klick setzt einen Punkt. Geschlossen wird mit einem Klick auf den
    /// ersten Punkt oder einem Doppelklick; die rechte Maustaste nimmt den letzten Punkt zurück,
    /// Esc verwirft das angefangene Vieleck. Solange „Vieleck" eingestellt ist, wählt ein Klick
    /// keine Fläche aus - dafür auf „Rechteck" zurückschalten.
    /// </para>
    /// <para>
    /// <b>Die Rechnung steht in <see cref="RevealFormen"/></b> (Schließen, Klemmen, Hülle) - hier
    /// liegt nur, was die Maus braucht. Der Entwurf wird nach jedem <c>Zeichne()</c> neu auf den
    /// Canvas gelegt, weil <c>Zeichne()</c> ihn leert.
    /// </para>
    /// </summary>
    public partial class RevealEditorView
    {
        private readonly List<Point> vieleck = new();
        private Polyline? vieleckLinie;
        private Line? vieleckGummiband;

        private bool VieleckAktiv => FormVieleck.IsChecked == true;

        /// <summary>Ein Klick im Vieleck-Werkzeug: Punkt setzen oder schließen.</summary>
        private void VieleckKlick(MouseButtonEventArgs e)
        {
            var lage = Bildlage();

            if (lage == null)
                return;

            var punkt = e.GetPosition(Buehne);
            var bisher = vieleck.Select(p => (p.X, p.Y)).ToList();

            var schliessen = e.ClickCount >= 2 && vieleck.Count >= 3
                || RevealFormen.SchliesstVieleck(bisher, (punkt.X, punkt.Y));

            if (!schliessen)
            {
                vieleck.Add(punkt);
                ZeichneVieleckEntwurf(punkt);
                return;
            }

            var (links, oben, breite, hoehe) = lage.Value;

            var flaeche = RevealFormen.Vieleck(bisher, new Bildlage(links, oben, breite, hoehe), aktuellerSchritt);

            vieleck.Clear();

            if (flaeche != null)
            {
                flaechen.Add(flaeche);
                gewaehlt = flaechen.Count - 1;
            }

            Zeichne();
        }

        private void Buehne_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!VieleckAktiv || vieleck.Count == 0)
                return;

            vieleck.RemoveAt(vieleck.Count - 1);
            Zeichne();

            e.Handled = true;
        }

        private void RevealEditorView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || vieleck.Count == 0)
                return;

            // Esc verwirft nur das angefangene Vieleck - und schliesst nicht das Fenster.
            vieleck.Clear();
            Zeichne();

            e.Handled = true;
        }

        /// <summary>Beim Wechsel der Form ein angefangenes Vieleck verwerfen.</summary>
        private void Form_Changed(object sender, RoutedEventArgs e)
        {
            if (vieleck.Count == 0 || Ueberdeckungen == null)
                return;

            vieleck.Clear();
            Zeichne();
        }

        /// <summary>Das Gummiband vom letzten Punkt zum Zeiger.</summary>
        private void VieleckMausBewegt(Point zeiger)
        {
            if (vieleckGummiband == null || vieleck.Count == 0)
                return;

            vieleckGummiband.X1 = vieleck[^1].X;
            vieleckGummiband.Y1 = vieleck[^1].Y;
            vieleckGummiband.X2 = zeiger.X;
            vieleckGummiband.Y2 = zeiger.Y;
        }

        /// <summary>
        /// Legt den Entwurf auf den Canvas: Linienzug, Punkte, der erste Punkt größer (dort
        /// schließt ein Klick), und das Gummiband.
        /// </summary>
        private void ZeichneVieleckEntwurf(Point? zeiger = null)
        {
            if (vieleckLinie != null)
                Ueberdeckungen.Children.Remove(vieleckLinie);

            if (vieleckGummiband != null)
                Ueberdeckungen.Children.Remove(vieleckGummiband);

            foreach (var alt in Ueberdeckungen.Children.OfType<Ellipse>().Where(el => Equals(el.Tag, "vieleck")).ToList())
                Ueberdeckungen.Children.Remove(alt);

            vieleckLinie = null;
            vieleckGummiband = null;

            if (vieleck.Count == 0)
                return;

            vieleckLinie = new Polyline
            {
                Points = new PointCollection(vieleck),
                Stroke = Brushes.Wheat,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
            };

            Ueberdeckungen.Children.Add(vieleckLinie);

            for (var i = 0; i < vieleck.Count; i++)
            {
                var gross = i == 0 ? RevealFormen.Fangradius : 4;

                var punkt = new Ellipse
                {
                    Width = gross * 2,
                    Height = gross * 2,
                    Stroke = Brushes.Wheat,
                    StrokeThickness = 2,
                    Fill = i == 0 ? new SolidColorBrush(Color.FromArgb(90, 255, 222, 173)) : Brushes.Wheat,
                    Tag = "vieleck",
                };

                System.Windows.Controls.Canvas.SetLeft(punkt, vieleck[i].X - gross);
                System.Windows.Controls.Canvas.SetTop(punkt, vieleck[i].Y - gross);

                Ueberdeckungen.Children.Add(punkt);
            }

            var bis = zeiger ?? vieleck[^1];

            vieleckGummiband = new Line
            {
                X1 = vieleck[^1].X,
                Y1 = vieleck[^1].Y,
                X2 = bis.X,
                Y2 = bis.Y,
                Stroke = Brushes.Wheat,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection([4, 3]),
            };

            Ueberdeckungen.Children.Add(vieleckGummiband);
        }
    }
}
