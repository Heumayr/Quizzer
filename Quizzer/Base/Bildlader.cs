using System.IO;
using System.Windows.Media.Imaging;

namespace Quizzer.Base
{
    /// <summary>
    /// Lädt ein Bild von der Platte, ohne zu werfen.
    /// <para>
    /// <b>Gemessen am 2026-09-06 nachts:</b> eine Datei, die WPF nicht dekodieren kann, lässt
    /// <c>BitmapImage.EndInit()</c> mit
    /// <c>NotSupportedException: Es wurde keine passende Imagingkomponente gefunden</c> werfen -
    /// und zwar mitten im Aufbau des Schrittes. Fünf Stellen taten das ungeschützt, drei davon
    /// auf dem Weg zum Beamer. Der Spielleiter bekam dafür ein Fehlerfenster vor den Gästen,
    /// statt den Schritt zu sehen.
    /// </para>
    /// <para>
    /// <b>Ein vorhandener Pfad genügt nicht als Prüfung.</b> <c>File.Exists</c> stand an drei
    /// dieser Stellen bereits davor - es sagt nur, dass eine Datei da ist, nichts darüber, ob
    /// WPF sie lesen kann. Auslöser sind eine <c>.webp</c>-Datei auf einem Rechner ohne dessen
    /// Codec (die Dateiauswahl bietet sie an), eine halb kopierte Datei, eine umbenannte.
    /// </para>
    /// <para>
    /// <c>StaticResources.LoadBrush</c> machte es von Anfang an richtig; von dort kommt das
    /// Muster.
    /// </para>
    /// </summary>
    internal static class Bildlader
    {
        /// <summary>
        /// Liefert das Bild, oder <c>null</c>, wenn es fehlt oder sich nicht lesen lässt. Wirft
        /// nie.
        /// </summary>
        internal static BitmapImage? Lade(string? pfad)
        {
            if (string.IsNullOrWhiteSpace(pfad) || !File.Exists(pfad))
                return null;

            try
            {
                var bild = new BitmapImage();

                bild.BeginInit();
                bild.UriSource = new Uri(pfad, UriKind.Absolute);
                bild.CacheOption = BitmapCacheOption.OnLoad;
                bild.EndInit();
                bild.Freeze();

                return bild;
            }
            catch (Exception)
            {
                // Bewusst jede Ausnahme: WPF wirft hier je nach Ursache NotSupportedException,
                // FileFormatException, IOException oder eine COM-Ausnahme aus dem Codec. Sie
                // einzeln aufzuzaehlen hiesse, beim naechsten Codec wieder das Fehlerfenster zu
                // bekommen - und der Aufrufer behandelt ohnehin nur "ging nicht".
                return null;
            }
        }
    }
}
