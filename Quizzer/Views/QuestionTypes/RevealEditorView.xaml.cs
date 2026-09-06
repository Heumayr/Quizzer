using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Die Aufdeckfrage einrichten: Bild wählen, Flächen darüberziehen oder die Unschärfe
    /// einstellen.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06:</b> „man lädt ein bild rein im frageeditor ... dann kann
    /// man nach und nach bildausschnitte verdecken (im editor flächen drüber zeichnen) ... bis
    /// das ganze bild verdeckt ist".
    /// </para>
    /// <para>
    /// <b>Die Flächen werden relativ zum Bild gespeichert</b>, nicht in Bildpunkten dieses
    /// Fensters - dasselbe Bild erscheint später auf dem Beamer, im Spielleiterfenster und in
    /// der Vorschau in drei verschiedenen Größen.
    /// </para>
    /// <para>
    /// <b>Gearbeitet wird auf einer Kopie.</b> „Abbrechen" muss wirklich nichts ändern; die
    /// Werte gehen erst bei „Übernehmen" an die Frage.
    /// </para>
    /// </summary>
    public partial class RevealEditorView : WindowBase
    {
        private readonly List<RevealArea> flaechen = new();

        private RevealQuestion? frage;
        private Point? zugStart;
        private System.Windows.Shapes.Rectangle? zugRechteck;

        /// <summary>Ob übernommen wurde.</summary>
        public bool Uebernommen { get; private set; }

        private RevealMode modus = RevealMode.Areas;
        private string bilddatei = string.Empty;
        private double unschaerfe = 40;

        public RevealEditorView()
        {
            InitializeComponent();
        }

        /// <summary>Stellt den Editor auf eine Frage ein.</summary>
        public void Zeige(RevealQuestion vorlage)
        {
            frage = vorlage;

            modus = vorlage.Mode;
            bilddatei = vorlage.ImageFileName;
            unschaerfe = vorlage.BlurStart <= 0 ? 40 : vorlage.BlurStart;

            flaechen.Clear();
            flaechen.AddRange(RevealAreas.Parse(vorlage.AreasJson));

            ModusFlaechen.IsChecked = modus == RevealMode.Areas;
            ModusUnschaerfe.IsChecked = modus == RevealMode.Blur;
            BlurSchieber.Value = unschaerfe;

            LadeBild();
            Zeichne();
        }

        private void LadeBild()
        {
            Bildname.Text = string.IsNullOrWhiteSpace(bilddatei)
                ? "Noch kein Bild gewählt."
                : bilddatei;

            if (string.IsNullOrWhiteSpace(bilddatei))
            {
                Bild.Source = null;
                return;
            }

            var pfad = Path.Combine(Settings.ResourceRootFolder, bilddatei);

            if (!File.Exists(pfad))
            {
                Bild.Source = null;
                Bildname.Text = bilddatei + " (liegt nicht im Datenordner)";

                return;
            }

            var bitmap = new BitmapImage();

            bitmap.BeginInit();
            bitmap.UriSource = new Uri(pfad);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            Bild.Source = bitmap;
        }

        /// <summary>Wo das Bild wirklich liegt - es sitzt mittig mit Rand.</summary>
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

        private void Zeichne()
        {
            Flaechen.Children.Clear();

            UnschaerfeBlock.Visibility = modus == RevealMode.Blur ? Visibility.Visible : Visibility.Collapsed;
            FlaechenBlock.Visibility = modus == RevealMode.Areas ? Visibility.Visible : Visibility.Collapsed;

            BlurWert.Text = $"Radius {unschaerfe:0}";
            FlaechenZahl.Text = $"{flaechen.Count} Fläche(n) - im Spiel {flaechen.Count} Schritt(e).";

            Bild.Effect = modus == RevealMode.Blur && unschaerfe > 0
                ? new BlurEffect { Radius = unschaerfe }
                : null;

            Warnung.Text = Warnungstext();

            if (modus != RevealMode.Areas)
                return;

            var lage = Bildlage();

            if (lage == null)
                return;

            var (links, oben, breite, hoehe) = lage.Value;
            var nummer = 0;

            foreach (var flaeche in flaechen)
            {
                nummer++;

                var rechteck = new System.Windows.Shapes.Rectangle
                {
                    Width = Math.Max(flaeche.W * breite, 0),
                    Height = Math.Max(flaeche.H * hoehe, 0),
                    Fill = new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)),
                    Stroke = Brushes.Wheat,
                    StrokeThickness = 1,
                };

                Canvas.SetLeft(rechteck, links + flaeche.X * breite);
                Canvas.SetTop(rechteck, oben + flaeche.Y * hoehe);

                Flaechen.Children.Add(rechteck);

                var beschriftung = new TextBlock
                {
                    Text = nummer.ToString(),
                    Foreground = Brushes.Wheat,
                    FontWeight = FontWeights.Bold,
                };

                Canvas.SetLeft(beschriftung, links + flaeche.X * breite + 4);
                Canvas.SetTop(beschriftung, oben + flaeche.Y * hoehe + 2);

                Flaechen.Children.Add(beschriftung);
            }
        }

        /// <summary>
        /// Sagt, was noch fehlt. Die wichtigste Zeile: ohne Bild ist die Frage nicht spielbar.
        /// </summary>
        private string Warnungstext()
        {
            if (string.IsNullOrWhiteSpace(bilddatei))
                return "Ohne Bild lässt sich diese Frage nicht spielen.";

            if (modus == RevealMode.Areas && flaechen.Count == 0)
                return "Noch keine Fläche gezogen - das Bild wäre von Anfang an ganz zu sehen.";

            return string.Empty;
        }

        private void BildWaehlen_Click(object sender, RoutedEventArgs e)
        {
            var quelle = FilePicker.AskForExistingFile(
                "Bild wählen",
                "Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Alle Dateien|*.*");

            if (quelle == null)
                return;

            // Ueber denselben Weg wie ein Schritt-Medium: die Datei landet im Ressourcenordner
            // und bekommt dort ihren Namen.
            var (dateiname, _) = FileHelper.HandleSelectedResourceFile(
                quelle, Settings.ResourceRootFolder);

            bilddatei = dateiname;

            LadeBild();
            Zeichne();
        }

        private void Modus_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            modus = ModusUnschaerfe.IsChecked == true ? RevealMode.Blur : RevealMode.Areas;

            Zeichne();
        }

        private void Blur_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            unschaerfe = e.NewValue;

            if (IsLoaded)
                Zeichne();
        }

        private void Buehne_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (modus != RevealMode.Areas || Bildlage() == null)
                return;

            zugStart = e.GetPosition(Buehne);

            zugRechteck = new System.Windows.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                Stroke = Brushes.Wheat,
                StrokeThickness = 1,
            };

            Flaechen.Children.Add(zugRechteck);
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

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            Uebernommen = false;
            Close();
        }

        private void Uebernehmen_Click(object sender, RoutedEventArgs e)
        {
            if (frage == null)
                return;

            frage.Mode = modus;
            frage.ImageFileName = bilddatei;
            frage.BlurStart = unschaerfe;
            frage.AreasJson = RevealAreas.ToJson(flaechen);

            Uebernommen = true;
            Close();
        }
    }
}
