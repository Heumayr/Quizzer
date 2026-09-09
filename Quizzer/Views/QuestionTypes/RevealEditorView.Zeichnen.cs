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

            // Anteil der Bildbreite statt Punkte der Anzeige (F13) - dieselbe Umrechnung wie im
            // Fragefenster, damit die Vorschau zeigt, was der Beamer zeigt.
            var breite = Bildlage()?.Breite ?? 0;

            if (art == RevealMode.Pixelate)
            {
                Bildraster.Zeige(
                    Bild, original, RevealAreas.Anzeigestaerke(staerke, breite), breite);
                return;
            }

            if (art == RevealMode.Blur)
                Bild.Effect = new BlurEffect { Radius = RevealAreas.Anzeigestaerke(staerke, breite) };
        }

        private string Buehnentext()
        {
            if (string.IsNullOrWhiteSpace(bilddatei))
                return "Wählen Sie rechts unter Schritt 2 ein Bild.";

            if (art == RevealMode.Areas)
            {
                var imSchritt = flaechen.Count(f => (f.Step ?? 0) == aktuellerSchritt);

                var kopf = $"Schritt {aktuellerSchritt + 1} von "
                    + $"{Math.Max(RevealAreas.Schrittzahl(flaechen), aktuellerSchritt + 1)} - "
                    + (imSchritt == 0
                        ? "noch keine Fläche. Ziehen Sie eine über das Bild."
                        : $"{imSchritt} Fläche(n) in diesem Schritt.");

                // Was gewaehlt ist, steht im Klartext daneben - die Randstaerke allein traegt das
                // nicht, und Information nur ueber die Darstellung ist ohnehin unzulaessig.
                return gewaehlt >= 0 && gewaehlt < flaechen.Count
                    ? kopf + $" Gewählt: Fläche {gewaehlt + 1} aus Schritt "
                        + $"{(flaechen[gewaehlt].Step ?? 0) + 1}."
                    : kopf + " Ein Klick ohne Ziehen wählt eine Fläche aus.";
            }

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
                var gruppen = flaechen
                    .GroupBy(f => f.Step ?? 0)
                    .OrderBy(g => g.Key)
                    .ToList();

                for (var i = 0; i < gruppen.Count; i++)
                {
                    var anzahl = gruppen[i].Count();

                    liste.Add(anzahl == 1
                        ? $"{i + 3}. eine Fläche fällt weg"
                        : $"{i + 3}. {anzahl} Flächen fallen zusammen weg");
                }
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

        /// <summary>
        /// Malt die Flächen über das Bild.
        /// <para>
        /// <b>Der laufende Schritt ist dreifach zu erkennen</b>, nie nur über Farbe: die Zahl auf
        /// jeder Fläche ist ihre <i>Schrittnummer</i> und wiederholt sich deshalb; die Flächen des
        /// laufenden Schritts haben einen dicken durchgezogenen Rand, fremde einen dünnen
        /// gestrichelten; und der Bühnentext sagt es in Worten.
        /// </para>
        /// <para>
        /// <b>Die Beschriftung sitzt auf dem Schwerpunkt der Ecken</b>, nicht auf der Ecke der
        /// Hülle: bei einem gedrehten Dreieck läge die dort außerhalb der Figur, unter Umständen
        /// über der Nachbarfläche.
        /// </para>
        /// </summary>
        private void ZeichneFlaechen()
        {
            var lage = Bildlage();

            if (lage == null)
                return;

            var (links, oben, breite, hoehe) = lage.Value;
            var bild = new Bildlage(links, oben, breite, hoehe);

            for (var i = 0; i < flaechen.Count; i++)
            {
                var flaeche = flaechen[i];
                var schritt = flaeche.Step ?? 0;
                var laeuft = schritt == aktuellerSchritt;
                var ecken = RevealFormen.EckenAnzeige(flaeche, bild);

                var vieleck = new System.Windows.Shapes.Polygon
                {
                    Fill = new SolidColorBrush(Color.FromArgb(laeuft ? (byte)200 : (byte)120, 0, 0, 0)),
                    Stroke = i == gewaehlt ? Brushes.Orange : Brushes.Wheat,
                    StrokeThickness = i == gewaehlt ? 3 : laeuft ? 2 : 1,
                    StrokeDashArray = laeuft ? null : [3, 3],
                };

                foreach (var (x, y) in ecken)
                    vieleck.Points.Add(new Point(x, y));

                Ueberdeckungen.Children.Add(vieleck);

                var beschriftung = new TextBlock
                {
                    Text = (schritt + 1).ToString(),
                    Foreground = Brushes.Wheat,
                    FontWeight = FontWeights.Bold,
                };

                Canvas.SetLeft(beschriftung, ecken.Average(e => e.X) - 5);
                Canvas.SetTop(beschriftung, ecken.Average(e => e.Y) - 9);

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

            if (art == RevealMode.Areas
                && !flaechen.Any(f => (f.Step ?? 0) == aktuellerSchritt))
                return $"In Schritt {aktuellerSchritt + 1} liegt keine Fläche. Beim Übernehmen "
                    + "fällt er weg.";

            return string.Empty;
        }
    }
}
