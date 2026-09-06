using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Was der Aufdeck-Editor auf die Bühne malt - Bild, Flächen, Schrittvorschau.
    /// <para>
    /// <b>Eigene Teildatei nach Thema</b>, wie <c>CurrentQuestionViewModel.ResultWindow.cs</c>:
    /// die Hauptdatei lag bei 475 Zeilen und damit dicht unter der Obergrenze von 500.
    /// </para>
    /// </summary>
    public partial class RevealEditorView
    {
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
    }
}
