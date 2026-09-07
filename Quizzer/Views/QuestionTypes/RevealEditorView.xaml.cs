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

        /// <summary>
        /// Der Schritt, in den neu gezeichnete Flächen fallen.
        /// <para>
        /// <b>Ein Feld, kein aus der Bühne gelesener Zustand</b> - <c>Zeichne()</c> leert den
        /// Canvas und baut die Schrittliste neu, und es läuft bei jeder Größenänderung. Stünde der
        /// laufende Schritt in der Oberfläche, spränge er beim Ziehen des Fensters zurück.
        /// </para>
        /// </summary>
        private int aktuellerSchritt;

        /// <summary>
        /// Welche Fläche ausgewählt ist, oder -1. Aus demselben Grund ein Feld wie
        /// <see cref="aktuellerSchritt"/>: sonst träfe der nächste Drehklick die falsche.
        /// </summary>
        private int gewaehlt = -1;

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

            // MitSchritt nagelt die Taktung fest, BEVOR jemand etwas entfernt. Ohne das haengt
            // der Schritt einer Altflaeche an ihrer Position - und das Loeschen der dritten von
            // fuenf verschoebe lautlos alle folgenden.
            flaechen.AddRange(RevealAreas.MitSchritt(RevealAreas.Parse(vorlage.AreasJson)));

            aktuellerSchritt = Math.Max(RevealAreas.Schrittzahl(flaechen) - 1, 0);
            gewaehlt = -1;

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

            var bitmap = Bildlader.Lade(pfad);

            if (bitmap == null)
            {
                original = null;
                Bild.Source = null;
                Bildname.Text = bilddatei + " (lässt sich nicht anzeigen)";

                return;
            }

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

        /// <summary>
        /// Wählt das Bild der Aufdeckfrage.
        /// <para>
        /// <b>Kein „Alle Dateien" mehr, und geprüft wird vor dem Kopieren.</b> Gemessen
        /// 2026-09-07: über „Alle Dateien" liess sich ein <c>.mp4</c> wählen. Es wurde in den
        /// Datenordner kopiert und als Bild der Frage eingetragen - danach war die Frage nicht
        /// mehr einzurichten, weil schon das Öffnen des Editors am Laden scheiterte.
        /// </para>
        /// <para>
        /// <b>Der Typ wird an der Quelle geprüft, nicht am Ergebnis.</b> Danach wäre die Datei
        /// bereits im Ressourcenordner und bliebe als Leiche liegen.
        /// <c>DetectResourceType</c> wirft bei einer unbekannten Endung - deshalb der
        /// <c>try</c>.
        /// </para>
        /// </summary>
        private void BildWaehlen_Click(object sender, RoutedEventArgs e)
        {
            var quelle = FilePicker.AskForExistingFile(
                "Bild wählen",
                "Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp");

            if (quelle == null)
                return;

            if (!IstBild(quelle))
            {
                UserPrompt.Inform(
                    "Das ist keine Bilddatei: " + Path.GetFileName(quelle)
                    + Environment.NewLine + Environment.NewLine
                    + "Eine Aufdeckfrage braucht ein Bild (PNG, JPG, BMP, GIF oder WEBP).",
                    "Bild wählen");

                return;
            }

            // Ueber denselben Weg wie ein Schritt-Medium: die Datei landet im Ressourcenordner
            // und bekommt dort ihren Namen.
            //
            // Die Endung stimmt hier bereits (IstBild hat geprueft) - der INHALT kann trotzdem
            // unlesbar sein. SkiaSharp wirft dann, und ohne diesen Faenger stuende ein
            // Fehlerfenster mit Stapelspur da. Gemessen 2026-09-07.
            string dateiname;

            try
            {
                (dateiname, _) = FileHelper.HandleSelectedResourceFile(
                    quelle, Settings.ResourceRootFolder);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException
                                          or UnauthorizedAccessException or NotSupportedException)
            {
                UserPrompt.Inform(ex.Message, "Bild wählen");

                return;
            }

            bilddatei = dateiname;

            LadeBild();
            Zeichne();
        }

        /// <summary>Ob die Datei nach ihrer Endung ein Bild ist. Wirft nicht.</summary>
        internal static bool IstBild(string pfad)
        {
            try
            {
                return FileHelper.DetectResourceType(pfad) == ResourceType.Image;
            }
            catch (NotSupportedException)
            {
                // Unbekannte Endung - erst recht kein Bild.
                return false;
            }
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

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            Uebernommen = false;
            Close();
        }

        /// <summary>
        /// Schreibt das Ergebnis in die Frage.
        /// <para>
        /// <b>Erst wenn feststeht, dass es auch gilt.</b> Bis 2026-09-07 schrieb der
        /// Übernehmen-Knopf die vier Felder sofort - die Rückfrage nach den wegfallenden
        /// Schritten kam aber erst danach. Wer dort „Nein" wählte, um seine Texte zu behalten,
        /// bekam trotzdem die neue Flächenaufteilung: die Frage trug dann drei Aufdeckschritte
        /// und fünf Inhaltsschritte, und am Quizabend zeigten die letzten beiden Bildschirme
        /// nichts Neues mehr.
        /// </para>
        /// <para>
        /// Der Klassenkommentar behauptete schon vorher „gearbeitet wird auf einer Kopie" -
        /// kopiert wurde aber nur die Flächenliste.
        /// </para>
        /// </summary>
        internal void SchreibNach(RevealQuestion ziel)
        {
            ziel.Mode = art;
            ziel.ImageFileName = bilddatei;
            ziel.BlurStart = staerke;

            // Normalisiert erzwingt die drei Zusagen, auf denen alles Weitere ruht: sortiert,
            // lueckenlos ab 0, mindestens eine Flaeche je Schritt - und zieht die Huelle nach.
            ziel.AreasJson = RevealAreas.ToJson(RevealAreas.Normalisiert(flaechen));
        }

        private void Uebernehmen_Click(object sender, RoutedEventArgs e)
        {
            if (frage == null)
                return;

            Uebernommen = true;
            Close();
        }

        /// <summary>Wie viele Inhaltsschritte die Einstellungen verlangen.</summary>
        internal int GewuenschteSchritte
            => art == RevealMode.Areas
                ? RevealAreas.Schrittzahl(RevealAreas.Normalisiert(flaechen))
                : weicheSchritte;
    }
}
