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
    /// Die Aufdeckfrage einrichten - in drei sichtbaren Schritten.
    /// <para>
    /// <b>Nutzerkritik vom 2026-09-06 abends:</b> „wo soll man das bild auswählen können und wie
    /// baut man die schritte im editor ... zuerst wählt man unschärfe verpixelt bzw. aufdecken
    /// ... aufdecken lässt mich dann selber überdeckungssteps zeichnen".
    /// </para>
    /// <para>
    /// <b>Das Zeichnen gab es schon</b> - was fehlte, war die Reihenfolge. Jetzt steht sie als
    /// „Schritt 1, 2, 3" da: erst die Art, dann das Bild, dann das Zeichnen beziehungsweise die
    /// Stärke. Und die Schritte, die dabei entstehen, stehen daneben - vorher musste man raten,
    /// was aus den Flächen wird.
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

        /// <summary>
        /// Das Bild in voller Auflösung. <b>Nötig, weil die Rasterung die Quelle ersetzt</b> - wer
        /// stattdessen <c>Bild.Source</c> weiterverwendet, rastert beim nächsten Zug das schon
        /// gerasterte Bild und landet nach ein paar Zügen bei vier Klötzchen.
        /// </summary>
        private BitmapSource? original;

        /// <summary>Ob übernommen wurde.</summary>
        public bool Uebernommen { get; private set; }

        private RevealMode art = RevealMode.Areas;
        private string bilddatei = string.Empty;
        private double staerke = 40;
        private int weicheSchritte = 4;

        public RevealEditorView()
        {
            InitializeComponent();
        }

        /// <summary>Stellt den Editor auf eine Frage ein.</summary>
        public void Zeige(RevealQuestion vorlage)
        {
            frage = vorlage;

            art = vorlage.Mode;
            bilddatei = vorlage.ImageFileName;
            staerke = vorlage.BlurStart <= 0 ? 40 : vorlage.BlurStart;

            flaechen.Clear();
            flaechen.AddRange(RevealAreas.Parse(vorlage.AreasJson));

            weicheSchritte = Math.Max(vorlage.Steps.Count(s => !s.IsStart && !s.IsFinish), 1);

            ArtAufdecken.IsChecked = art == RevealMode.Areas;
            ArtUnschaerfe.IsChecked = art == RevealMode.Blur;
            ArtVerpixelt.IsChecked = art == RevealMode.Pixelate;

            StaerkeSchieber.Value = staerke;
            SchritteSchieber.Value = Math.Clamp(weicheSchritte, 1, 10);

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
                original = null;
                Bild.Source = null;

                return;
            }

            var pfad = Path.Combine(Settings.ResourceRootFolder, bilddatei);

            if (!File.Exists(pfad))
            {
                original = null;
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

            original = bitmap;
            Bild.Source = bitmap;
        }

        /// <summary>Wo das Bild wirklich liegt - es sitzt mittig mit Rand.</summary>
        private (double Links, double Oben, double Breite, double Hoehe)? Bildlage()
        {
            if (original is not BitmapSource quelle || Buehne.ActualWidth <= 0)
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
            Ueberdeckungen.Children.Clear();

            var weich = art != RevealMode.Areas;

            StaerkeBlock.Visibility = weich ? Visibility.Visible : Visibility.Collapsed;
            ZeichenBlock.Visibility = weich ? Visibility.Collapsed : Visibility.Visible;

            SchrittDreiTitel.Text = weich
                ? "Schritt 3 — Stärke und Anzahl"
                : "Schritt 3 — Flächen zeichnen";

            StaerkeTitel.Text = art == RevealMode.Pixelate
                ? "Wie grob am Anfang?"
                : "Wie unscharf am Anfang?";

            StaerkeWert.Text = $"Stärke {staerke:0}";
            SchritteWert.Text = $"{weicheSchritte} Schritt(e) bis zum klaren Bild";

            Buehnenhinweis.Text = Buehnentext();

            Zeigebild();

            Schrittliste.ItemsSource = Schrittvorschau();
            Warnung.Text = Warnungstext();

            if (weich)
                return;

            ZeichneFlaechen();
        }

        /// <summary>
        /// Legt das Bild so hin, wie der erste Schritt es zeigt - weichgezeichnet, gerastert oder
        /// klar.
        /// <para>
        /// <b>Die Rasterung läuft über dieselbe Stelle wie im Spiel</b> (<see cref="Bildraster"/>).
        /// Eine Vorschau, die anders aussieht als der Beamer, ist keine - vorher stand hier eine
        /// starke Weichzeichnung als Andeutung.
        /// </para>
        /// </summary>
        private void Zeigebild()
        {
            Bild.Effect = null;

            RenderOptions.SetBitmapScalingMode(Bild, BitmapScalingMode.Unspecified);
            Bild.Source = original;

            if (original == null || staerke <= 0)
                return;

            if (art == RevealMode.Pixelate)
            {
                Bildraster.Zeige(Bild, original, staerke, Bildlage()?.Breite ?? 0);
                return;
            }

            if (art == RevealMode.Blur)
                Bild.Effect = new BlurEffect { Radius = staerke };
        }

        private string Buehnentext()
        {
            if (string.IsNullOrWhiteSpace(bilddatei))
                return "Wählen Sie rechts unter Schritt 2 ein Bild.";

            if (art == RevealMode.Areas)
                return flaechen.Count == 0
                    ? "Ziehen Sie mit der Maus eine Fläche über das Bild."
                    : $"{flaechen.Count} Fläche(n) gezeichnet - die Zahl zeigt die Reihenfolge.";

            return "So sieht das Bild im ersten Schritt aus.";
        }

        /// <summary>Was aus den Einstellungen an Schritten wird - in Worten.</summary>
        private List<string> Schrittvorschau()
        {
            var liste = new List<string>
            {
                "1. Startbildschirm — nur die Fragenart",
                "2. Fragebildschirm — nur die Frage",
            };

            if (art == RevealMode.Areas)
            {
                for (var i = 0; i < flaechen.Count; i++)
                    liste.Add($"{i + 3}. Fläche {i + 1} fällt weg");
            }
            else
            {
                var wort = art == RevealMode.Pixelate ? "feiner" : "schärfer";

                for (var i = 0; i < weicheSchritte; i++)
                    liste.Add($"{i + 3}. Bild wird {wort}");
            }

            liste.Add($"{liste.Count + 1}. Abschluss — die Auflösung");

            return liste;
        }

        private void ZeichneFlaechen()
        {
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
                    Fill = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                    Stroke = Brushes.Wheat,
                    StrokeThickness = 1,
                };

                Canvas.SetLeft(rechteck, links + flaeche.X * breite);
                Canvas.SetTop(rechteck, oben + flaeche.Y * hoehe);

                Ueberdeckungen.Children.Add(rechteck);

                var beschriftung = new TextBlock
                {
                    Text = nummer.ToString(),
                    Foreground = Brushes.Wheat,
                    FontWeight = FontWeights.Bold,
                };

                Canvas.SetLeft(beschriftung, links + flaeche.X * breite + 4);
                Canvas.SetTop(beschriftung, oben + flaeche.Y * hoehe + 2);

                Ueberdeckungen.Children.Add(beschriftung);
            }
        }

        /// <summary>Sagt, was noch fehlt - die wichtigste Zeile zuerst.</summary>
        private string Warnungstext()
        {
            if (string.IsNullOrWhiteSpace(bilddatei))
                return "Ohne Bild lässt sich diese Frage nicht spielen.";

            if (art == RevealMode.Areas && flaechen.Count == 0)
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

        private void Art_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            art = ArtUnschaerfe.IsChecked == true ? RevealMode.Blur
                : ArtVerpixelt.IsChecked == true ? RevealMode.Pixelate
                : RevealMode.Areas;

            Zeichne();
        }

        private void Staerke_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            staerke = e.NewValue;

            if (IsLoaded)
                Zeichne();
        }

        private void Schritte_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            weicheSchritte = (int)e.NewValue;

            if (IsLoaded)
                Zeichne();
        }

        /// <summary>
        /// Nach jeder Größenänderung neu zeichnen.
        /// <para>
        /// <b>Auch für den ersten Aufbau nötig:</b> <see cref="Zeige"/> läuft vor
        /// <c>ShowDialog</c>, und da ist die Bühne noch null breit - Flächen und Rasterung
        /// hätten nichts, worauf sie sich beziehen könnten.
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

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            Uebernommen = false;
            Close();
        }

        private void Uebernehmen_Click(object sender, RoutedEventArgs e)
        {
            if (frage == null)
                return;

            frage.Mode = art;
            frage.ImageFileName = bilddatei;
            frage.BlurStart = staerke;
            frage.AreasJson = RevealAreas.ToJson(flaechen);

            Uebernommen = true;
            Close();
        }

        /// <summary>Wie viele Inhaltsschritte die Einstellungen verlangen.</summary>
        internal int GewuenschteSchritte
            => art == RevealMode.Areas ? flaechen.Count : weicheSchritte;
    }
}
