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
    /// Die Aufdeckfrage auf dem Bildschirm - Flächen fallen weg, oder die Unschärfe nimmt ab.
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

            var gezeigt = Kontext.PreviousStepsCount;

            if (frage.Mode == RevealMode.Blur)
            {
                var radius = RevealAreas.Unschaerfe(
                    frage.BlurStart, Kontext.LayoutReferenceSteps.Length, gezeigt);

                Bild.Effect = radius > 0 ? new BlurEffect { Radius = radius } : null;

                // Der Spielleiter soll sehen, wo er steht - die Mitspieler nicht.
                if (IsMasterView)
                    Hinweis.Text = radius > 0 ? $"Unschärfe {radius:0}" : "Bild scharf";

                return;
            }

            ZeichneFlaechen(RevealAreas.Parse(frage.AreasJson), gezeigt);

            if (IsMasterView)
            {
                var gesamt = RevealAreas.Parse(frage.AreasJson).Count;

                Hinweis.Text = $"{Math.Min(gezeigt, gesamt)} von {gesamt} Flächen aufgedeckt";
            }
        }

        /// <summary>Lädt das Bild. Meldet, ob überhaupt eines da ist.</summary>
        private bool LadeBild(RevealQuestion frage)
        {
            if (string.IsNullOrWhiteSpace(frage.ImageFileName))
            {
                Bild.Source = null;
                Hinweis.Text = "Für diese Aufdeckfrage ist noch kein Bild hinterlegt.";

                return false;
            }

            var pfad = Path.Combine(Settings.ResourceRootFolder, frage.ImageFileName);

            if (!File.Exists(pfad))
            {
                Bild.Source = null;
                Hinweis.Text = "Das Bild dieser Frage liegt nicht im Datenordner.";

                return false;
            }

            var bitmap = new BitmapImage();

            bitmap.BeginInit();
            bitmap.UriSource = new Uri(pfad);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            Bild.Source = bitmap;

            return true;
        }

        /// <summary>
        /// Zeichnet die noch verdeckenden Flächen - auf die Stelle, an der das Bild wirklich
        /// liegt.
        /// </summary>
        private void ZeichneFlaechen(IReadOnlyList<RevealArea> alle, int aufgedeckt)
        {
            if (Bild.Source is not BitmapSource quelle || Buehne.ActualWidth <= 0)
                return;

            // Stretch=Uniform: das Bild sitzt mittig, mit Rand oben/unten oder links/rechts.
            var faktor = Math.Min(
                Buehne.ActualWidth / quelle.PixelWidth,
                Buehne.ActualHeight / quelle.PixelHeight);

            var breite = quelle.PixelWidth * faktor;
            var hoehe = quelle.PixelHeight * faktor;
            var links = (Buehne.ActualWidth - breite) / 2;
            var oben = (Buehne.ActualHeight - hoehe) / 2;

            foreach (var flaeche in RevealAreas.NochVerdeckt(alle, aufgedeckt))
            {
                var rechteck = new System.Windows.Shapes.Rectangle
                {
                    Width = Math.Max(flaeche.W * breite, 0),
                    Height = Math.Max(flaeche.H * hoehe, 0),
                    Fill = Brushes.Black,
                };

                Canvas.SetLeft(rechteck, links + flaeche.X * breite);
                Canvas.SetTop(rechteck, oben + flaeche.Y * hoehe);

                Flaechen.Children.Add(rechteck);
            }
        }
    }
}
