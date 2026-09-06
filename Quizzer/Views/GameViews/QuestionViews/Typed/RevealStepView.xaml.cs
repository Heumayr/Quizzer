using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Quizzer.Views.GameViews.QuestionViews.Typed
{
    /// <summary>
    /// Die Aufdeckfrage auf dem Bildschirm - Flächen fallen weg, oder Unschärfe und Raster
    /// nehmen ab.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06.</b> Die Rechnung, was auf welchem Schritt zu sehen ist,
    /// steht in <see cref="RevealAreas"/> - hier wird sie nur gezeichnet.
    /// </para>
    /// <para>
    /// <b>Die Flächen werden im Code gesetzt, nicht im Markup.</b> Sie sind relativ zum Bild
    /// angegeben, und wie groß das Bild wirklich ausgelegt wird, weiß erst das Layout: dasselbe
    /// Bild erscheint auf dem Beamer, im Spielleiterfenster und in der Vorschau in drei
    /// verschiedenen Größen.
    /// </para>
    /// </summary>
    public partial class RevealStepView : UserControlStepViewBase
    {
        public RevealStepView()
        {
            InitializeComponent();

            DataContextChanged += (_, _) => Zeichne();
            SizeChanged += (_, _) => Zeichne();
            Loaded += (_, _) => Zeichne();
        }

        private QuestionStepViewContext? Kontext => DataContext as QuestionStepViewContext;

        private void Zeichne()
        {
            Flaechen.Children.Clear();
            Bild.Effect = null;
            Hinweis.Text = string.Empty;

            if (Kontext?.Question is not RevealQuestion frage)
                return;

            if (!LadeBild(frage))
                return;

            var gezeigt = GezeigteSchritte();
            var schritte = Kontext.LayoutReferenceSteps.Length;

            if (frage.Mode == RevealMode.Areas)
            {
                var flaechen = RevealAreas.Parse(frage.AreasJson);

                ZeichneFlaechen(flaechen, gezeigt);

                // Gezaehlt werden SCHRITTE, nicht Flaechen - seit Meldung 23 duerfen mehrere
                // Flaechen zusammen fallen.
                var aufdeckschritte = RevealAreas.Schrittzahl(flaechen);

                Melde($"{Math.Min(gezeigt, aufdeckschritte)} von {aufdeckschritte} "
                    + "Aufdeckschritten gezeigt");

                return;
            }

            VerschleiereGanzesBild(frage, schritte, gezeigt);
        }

        /// <summary>
        /// Unschärfe und Raster - beide verschleiern das <b>ganze</b> Bild und nehmen je Schritt
        /// gleichmäßig ab; verdeckt wird nichts.
        /// </summary>
        private void VerschleiereGanzesBild(RevealQuestion frage, int schritte, int gezeigt)
        {
            if (frage.Mode == RevealMode.Pixelate)
            {
                var kante = RevealAreas.Rasterung(frage.BlurStart, schritte, gezeigt);

                Bildraster.Zeige(Bild, Bild.Source as BitmapSource, kante, Bildlage()?.Breite ?? 0);

                Melde(kante > 0 ? $"Raster {kante:0}" : "Bild scharf");

                return;
            }

            var radius = RevealAreas.Unschaerfe(frage.BlurStart, schritte, gezeigt);

            Bild.Effect = radius > 0 ? new BlurEffect { Radius = radius } : null;

            Melde(radius > 0 ? $"Unschärfe {radius:0}" : "Bild scharf");
        }

        /// <summary>
        /// Wie viele Aufdeckschritte <b>bis einschließlich diesem</b> gezeigt sind.
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> hier stand <c>PreviousStepsCount</c>, also die Schritte
        /// <i>davor</i>. Auf dem ersten Aufdeckschritt fiel damit gar nichts, und die letzte
        /// Fläche fiel erst auf dem Auflösungsbildschirm - solange geraten werden konnte, war
        /// das Bild also nie ganz zu sehen. Bei drei Aufdeckschritten deckten nur zwei etwas
        /// auf. Bei Unschärfe dasselbe: scharf wurde es erst auf der Auflösung.
        /// </para>
        /// <para>
        /// <b>Derselbe Versatz wie beim Punkteabzug</b>, der am selben Tag gefunden wurde - und
        /// dieselbe Regel: Start- und Fragebildschirm zählen nicht mit, der Abschluss bleibt bei
        /// der vollen Zahl.
        /// </para>
        /// </summary>
        private int GezeigteSchritte()
        {
            var schritt = Kontext?.Step;

            if (schritt == null)
                return 0;

            var zaehltMit = !schritt.IsStart && !schritt.IsFinish && !schritt.IsQuestionOnly;

            return zaehltMit
                ? (Kontext?.PreviousStepsCount ?? 0) + 1
                : Kontext?.PreviousStepsCount ?? 0;
        }

        /// <summary>Der Spielleiter soll sehen, wo er steht - die Mitspieler nicht.</summary>
        private void Melde(string text)
        {
            if (IsMasterView)
                Hinweis.Text = text;
        }

        /// <summary>
        /// Meldet ein fehlendes oder unlesbares Bild - <b>nur dem Spielleiter</b>.
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> die drei Meldungen schrieben direkt an das Hinweisfeld und
        /// damit an <see cref="Melde"/> vorbei. Das Beamerfenster benutzt dieselbe Ansicht, also
        /// stand mitten im Bild vor den Gästen ein Satz, der für den Spielleiter gedacht ist -
        /// etwa „Das Bild dieser Frage liegt nicht im Datenordner."
        /// </para>
        /// <para>
        /// Auf dem Beamer bleibt die Zeile leer. Ein neutraler Ersatztext wäre schlechter: die
        /// Mitspieler sollen gar nicht merken, dass etwas fehlt, sonst raten sie darüber statt
        /// über die Frage.
        /// </para>
        /// </summary>
        private void MeldeFehlendesBild(string text)
        {
            Bild.Source = null;

            Melde(IsMasterView ? text : string.Empty);
        }

        /// <summary>Lädt das Bild. Meldet, ob überhaupt eines da ist.</summary>
        private bool LadeBild(RevealQuestion frage)
        {
            if (string.IsNullOrWhiteSpace(frage.ImageFileName))
            {
                MeldeFehlendesBild("Für diese Aufdeckfrage ist noch kein Bild hinterlegt.");

                return false;
            }

            var pfad = Path.Combine(Settings.ResourceRootFolder, frage.ImageFileName);

            if (!File.Exists(pfad))
            {
                MeldeFehlendesBild("Das Bild dieser Frage liegt nicht im Datenordner.");

                return false;
            }

            var bitmap = Bildlader.Lade(pfad);

            if (bitmap == null)
            {
                // Die Datei liegt da, WPF kann sie nur nicht lesen - etwa eine .webp auf einem
                // Rechner ohne deren Codec. Ohne diesen Zweig warf der Schrittaufbau.
                MeldeFehlendesBild("Das Bild dieser Frage lässt sich nicht anzeigen.");

                return false;
            }

            // Eine vorherige Rasterung darf nicht am Steuerelement hängenbleiben - sonst zeigt der
            // nächste Schritt das scharfe Bild in Klötzchen.
            RenderOptions.SetBitmapScalingMode(Bild, BitmapScalingMode.Unspecified);

            Bild.Source = bitmap;

            return true;
        }

        /// <summary>
        /// Wo das Bild wirklich liegt. <c>Stretch=Uniform</c>: es sitzt mittig, mit Rand oben und
        /// unten oder links und rechts.
        /// </summary>
        private (double Links, double Oben, double Breite, double Hoehe)? Bildlage()
        {
            if (Bild.Source is not BitmapSource quelle || Buehne.ActualWidth <= 0)
                return null;

            var faktor = Math.Min(
                Buehne.ActualWidth / quelle.PixelWidth,
                Buehne.ActualHeight / quelle.PixelHeight);

            var breite = quelle.PixelWidth * faktor;
            var hoehe = quelle.PixelHeight * faktor;

            return ((Buehne.ActualWidth - breite) / 2, (Buehne.ActualHeight - hoehe) / 2, breite, hoehe);
        }

        /// <summary>
        /// Zeichnet die noch verdeckenden Flächen - auf die Stelle, an der das Bild wirklich
        /// liegt.
        /// </summary>
        private void ZeichneFlaechen(IReadOnlyList<RevealArea> alle, int aufgedeckt)
        {
            var lage = Bildlage();

            if (lage == null)
                return;

            var (links, oben, breite, hoehe) = lage.Value;
            var bild = new Bildlage(links, oben, breite, hoehe);

            foreach (var flaeche in RevealAreas.NochVerdeckt(alle, aufgedeckt))
            {
                // Ein Vieleck, kein Rechteck mit Transformation: die Ecken stehen so, wie der
                // Editor sie hingeschrieben hat. Fuer eine Altflaeche ohne Ecken ist es dasselbe
                // schwarze Viereck an derselben Stelle.
                var vieleck = new System.Windows.Shapes.Polygon { Fill = Brushes.Black };

                foreach (var (x, y) in RevealFormen.EckenAnzeige(flaeche, bild))
                    vieleck.Points.Add(new Point(x, y));

                Flaechen.Children.Add(vieleck);
            }
        }
    }
}
