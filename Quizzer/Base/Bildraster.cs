using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quizzer.Base
{
    /// <summary>
    /// Verpixelt ein Bild - klein rechnen, ohne Glättung wieder groß ziehen.
    /// <para>
    /// <b>WPF hat keinen Raster-Effekt.</b> Das Herunterrechnen <i>ist</i> der Effekt, und sichtbar
    /// wird er erst durch <see cref="BitmapScalingMode.NearestNeighbor"/> am Ziel - ohne das glättet
    /// WPF beim Hochziehen alles wieder weg, und man sieht eine Unschärfe statt eines Rasters.
    /// </para>
    /// <para>
    /// <b>Eine Stelle für Editor und Spiel.</b> Zeigte der Editor eine andere Rasterung als der
    /// Beamer, wäre die Vorschau wertlos - genau der Grund, aus dem auch die Rechnung selbst in
    /// <c>RevealAreas</c> steht und nicht in den Ansichten.
    /// </para>
    /// </summary>
    internal static class Bildraster
    {
        /// <summary>
        /// Rastert das Bild. Gibt <paramref name="quelle"/> unverändert zurück, wenn nichts zu
        /// rastern ist - daran erkennt der Aufrufer, dass er nicht auf NearestNeighbor umstellen
        /// muss.
        /// </summary>
        /// <param name="quelle">Das Bild in voller Auflösung.</param>
        /// <param name="blockkante">Kantenlänge eines Blocks in Punkten der Anzeige; 0 heißt scharf.</param>
        /// <param name="anzeigebreite">Wie breit das Bild ausgelegt wird.</param>
        internal static BitmapSource? Rastere(
            BitmapSource? quelle, double blockkante, double anzeigebreite)
        {
            if (quelle == null || blockkante <= 0 || anzeigebreite <= 0)
                return quelle;

            // Mindestens zwei Blöcke - bei einem einzigen wäre das Bild eine einfarbige Fläche,
            // und die trägt keinen Hinweis mehr.
            var bloecke = Math.Max(2, (int)Math.Round(anzeigebreite / blockkante));

            if (bloecke >= quelle.PixelWidth)
                return quelle;

            var schrumpf = (double)bloecke / quelle.PixelWidth;

            var klein = new TransformedBitmap(quelle, new ScaleTransform(schrumpf, schrumpf));

            klein.Freeze();

            return klein;
        }

        /// <summary>Legt das gerasterte Bild ins Ziel und stellt die Skalierungsart passend ein.</summary>
        internal static void Zeige(
            Image ziel, BitmapSource? quelle, double blockkante, double anzeigebreite)
        {
            var gezeigt = Rastere(quelle, blockkante, anzeigebreite);

            RenderOptions.SetBitmapScalingMode(
                ziel,
                ReferenceEquals(gezeigt, quelle)
                    ? BitmapScalingMode.Unspecified
                    : BitmapScalingMode.NearestNeighbor);

            ziel.Source = gezeigt;
        }
    }
}
