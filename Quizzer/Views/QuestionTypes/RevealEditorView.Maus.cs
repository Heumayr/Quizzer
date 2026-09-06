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

            // Ein Klick ohne Ziehen ist keine Flaeche.
            if (w < 0.01 || h < 0.01)
            {
                Zeichne();
                return;
            }

            flaechen.Add(new RevealArea(
                Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1),
                Math.Clamp(w, 0, 1), Math.Clamp(h, 0, 1)));

            Zeichne();
        }

        private void LetzteEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (flaechen.Count > 0)
                flaechen.RemoveAt(flaechen.Count - 1);

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
