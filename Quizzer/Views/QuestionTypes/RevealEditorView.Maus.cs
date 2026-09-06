using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Die Maus über der Bühne: ziehen, auswählen, entfernen.
    /// <para>
    /// <b>Eigene Teildatei nach Thema</b> - Begründung siehe <c>.Zeichnen.cs</c>.
    /// </para>
    /// </summary>
    public partial class RevealEditorView
    {
        /// <summary>
        /// Nach jeder Größenänderung neu zeichnen.
        /// <para>
        /// <b>Auch für den ersten Aufbau nötig:</b> <c>Zeige</c> läuft vor <c>ShowDialog</c>, und
        /// da ist die Bühne noch null breit - Flächen hätten nichts, worauf sie sich beziehen.
        /// </para>
        /// </summary>
        private void Buehne_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (frage != null)
                Zeichne();
        }

        private void Buehne_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (art != RevealMode.Areas || Bildlage() == null)
                return;

            zugStart = e.GetPosition(Buehne);

            zugRechteck = new System.Windows.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                Stroke = Brushes.Wheat,
                StrokeThickness = 1,
            };

            Ueberdeckungen.Children.Add(zugRechteck);
            Buehne.CaptureMouse();
        }

        private void Buehne_MouseMove(object sender, MouseEventArgs e)
        {
            if (zugStart == null || zugRechteck == null)
                return;

            var jetzt = e.GetPosition(Buehne);
            var start = zugStart.Value;

            Canvas.SetLeft(zugRechteck, Math.Min(start.X, jetzt.X));
            Canvas.SetTop(zugRechteck, Math.Min(start.Y, jetzt.Y));

            zugRechteck.Width = Math.Abs(jetzt.X - start.X);
            zugRechteck.Height = Math.Abs(jetzt.Y - start.Y);
        }

        private void Buehne_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Buehne.ReleaseMouseCapture();

            if (zugStart == null || zugRechteck == null)
                return;

            var lage = Bildlage();
            var start = zugStart.Value;
            var ende = e.GetPosition(Buehne);

            zugStart = null;
            zugRechteck = null;

            if (lage == null)
                return;

            var (links, oben, breite, hoehe) = lage.Value;

            var x = (Math.Min(start.X, ende.X) - links) / breite;
            var y = (Math.Min(start.Y, ende.Y) - oben) / hoehe;
            var w = Math.Abs(ende.X - start.X) / breite;
            var h = Math.Abs(ende.Y - start.Y) / hoehe;

            // Ein Klick ohne Ziehen legt keine Flaeche an - er WAEHLT eine aus. Damit kollidiert
            // das Auswaehlen nie mit dem Ziehen, und es gibt keinen Moduswechsel.
            if (w < 0.01 || h < 0.01)
            {
                WaehleUnterDemZeiger(ende, links, oben, breite, hoehe);

                Zeichne();

                return;
            }

            // Waagrecht und senkrecht zusammen klemmen, nicht einzeln: sonst kann X+W groesser
            // als 1 werden, die Flaeche ragt aus dem Bild und ist dort nicht mehr anklickbar.
            var gx = Math.Clamp(x, 0, 1);
            var gy = Math.Clamp(y, 0, 1);
            var gw = Math.Clamp(w, 0, 1 - gx);
            var gh = Math.Clamp(h, 0, 1 - gy);

            flaechen.Add(ArtVonForm(gx, gy, gw, gh, aktuellerSchritt));

            gewaehlt = flaechen.Count - 1;

            Zeichne();
        }

        /// <summary>Rechteck oder Dreieck - je nachdem, was oben eingestellt ist.</summary>
        private RevealArea ArtVonForm(double x, double y, double w, double h, int schritt)
            => FormDreieck.IsChecked == true
                ? RevealFormen.Dreieck(x, y, w, h, schritt)
                : RevealFormen.Rechteck(x, y, w, h, schritt);

        /// <summary>
        /// Wählt die Fläche unter dem Zeiger - und bei mehreren übereinander <b>reihum</b>.
        /// <para>
        /// <b>„Die oberste gewinnt" allein macht eine vollständig überdeckte Fläche
        /// unerreichbar.</b> Deshalb sucht ein Klick an derselben Stelle unterhalb der aktuellen
        /// Auswahl weiter und läuft am Ende um. Es ist der einzige Weg zu einer verdeckten Fläche.
        /// </para>
        /// </summary>
        private void WaehleUnterDemZeiger(
            Point punkt, double links, double oben, double breite, double hoehe)
        {
            var lage = new Bildlage(links, oben, breite, hoehe);

            var treffer = Enumerable.Range(0, flaechen.Count)
                .Where(i => RevealFormen.Trifft(flaechen[i], punkt.X, punkt.Y, lage))
                .ToList();

            if (treffer.Count == 0)
            {
                gewaehlt = -1;
                return;
            }

            var stelle = treffer.IndexOf(gewaehlt);

            gewaehlt = treffer[(stelle + 1) % treffer.Count];
        }

        /// <summary>
        /// Entfernt die ausgewählte Fläche - ohne Auswahl die letzte des laufenden Schritts.
        /// </summary>
        private void AusgewaehlteEntfernen_Click(object sender, RoutedEventArgs e)
        {
            var weg = gewaehlt >= 0 && gewaehlt < flaechen.Count
                ? gewaehlt
                : flaechen.FindLastIndex(f => (f.Step ?? 0) == aktuellerSchritt);

            if (weg >= 0)
                flaechen.RemoveAt(weg);

            gewaehlt = -1;

            Zeichne();
        }

        /// <summary>
        /// Dreht die ausgewählte Fläche - <b>zwei Knöpfe, kein Schieber</b>.
        /// <para>
        /// <c>Zeichne()</c> läuft bei jeder Größenänderung, und ein Schieber, den <c>Zeichne()</c>
        /// aus der Auswahl setzt, feuert dabei sein <c>ValueChanged</c> und ruft <c>Zeichne()</c>
        /// erneut. Das erscheint nicht als Ausnahme, sondern als hängendes Fenster. Zwei Knöpfe
        /// haben diese Rückkopplung nicht.
        /// </para>
        /// </summary>
        private void Drehen_Click(object sender, RoutedEventArgs e)
        {
            var lage = Bildlage();

            if (lage == null || gewaehlt < 0 || gewaehlt >= flaechen.Count)
                return;

            var grad = (sender as FrameworkElement)?.Tag is string t
                       && double.TryParse(t, System.Globalization.CultureInfo.InvariantCulture,
                           out var wert)
                ? wert
                : 15;

            var (links, oben, breite, hoehe) = lage.Value;

            flaechen[gewaehlt] = RevealFormen.Gedreht(
                flaechen[gewaehlt], grad, new Bildlage(links, oben, breite, hoehe));

            Zeichne();
        }

        /// <summary>
        /// Zum nächsten Aufdeckschritt. Erst möglich, wenn im laufenden mindestens eine Fläche
        /// liegt - sonst entstünde ein Bildschirm, auf dem sichtbar nichts geschieht.
        /// </summary>
        private void NaechsterSchritt_Click(object sender, RoutedEventArgs e)
        {
            if (!flaechen.Any(f => (f.Step ?? 0) == aktuellerSchritt))
            {
                UserPrompt.Inform(
                    "In diesem Schritt liegt noch keine Fläche. Ein Schritt ohne Fläche wäre ein "
                    + "Bildschirm, auf dem sichtbar nichts passiert.",
                    "Aufdeckfrage");

                return;
            }

            aktuellerSchritt++;
            gewaehlt = -1;

            Zeichne();
        }

        /// <summary>Einen Schritt zurück, um dort nachzuzeichnen.</summary>
        private void VorigerSchritt_Click(object sender, RoutedEventArgs e)
        {
            if (aktuellerSchritt == 0)
                return;

            aktuellerSchritt--;
            gewaehlt = -1;

            Zeichne();
        }

        private void AlleEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (flaechen.Count == 0)
                return;

            if (!UserPrompt.Confirm(
                    $"Alle {flaechen.Count} Flächen entfernen?", "Aufdeckfrage"))
                return;

            flaechen.Clear();

            Zeichne();
        }
    }
}
