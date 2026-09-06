using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Quizzer.DataModels.Questions.Schrittbau
{
    /// <summary>
    /// Eine Zeile in der Maske - und genau ein <see cref="QuestionStepResource"/> dahinter.
    /// <para>
    /// <b>Der Schritt wird durchgereicht, nie neu gebaut.</b> Das ist die tragende Regel des
    /// ganzen Umbaus: <c>SaveWithStepsAsync</c> löscht jeden gespeicherten Schritt, dessen Id
    /// nicht in der übergebenen Liste steht. Ein Übersetzer, der beim Lesen ein Feld fallen
    /// lässt, löscht es beim nächsten Speichern <b>ohne Meldung</b>. Was die Maske nicht
    /// modelliert - Medium, Startschritt-Kennung, alles Künftige -, fährt auf demselben Objekt
    /// unberührt mit.
    /// </para>
    /// <para>
    /// <b>WPF-frei</b>, aus demselben Grund wie <see cref="QuestionValidator"/>: so bleibt jede
    /// Regel ohne Oberflächen-Thread prüfbar.
    /// </para>
    /// </summary>
    public sealed class StepZeile : INotifyPropertyChanged
    {
        /// <summary>Nimmt einen vorhandenen Schritt auf.</summary>
        public StepZeile(QuestionStepResource schritt)
        {
            Schritt = schritt ?? throw new ArgumentNullException(nameof(schritt));
        }

        /// <summary>
        /// Der Schritt selbst. <b>Dieselbe Instanz wie in <c>Question.Steps</c></b> - wer hier
        /// eine neue baut, hebelt die tragende Regel aus.
        /// </summary>
        public QuestionStepResource Schritt { get; }

        /// <summary>
        /// Ob diese Zeile angefasst wurde.
        /// <para>
        /// <b>Sie sitzt an der Zeile, nicht am Modell.</b> Zwei Dinge hängen daran: ein
        /// abgeleiteter Vorschlagstext hört auf, sich zu erneuern, sobald jemand hineintippt -
        /// und <see cref="Models.ModelBase.Designation"/> wird nur dann mitgezogen, wenn wirklich
        /// jemand geschrieben hat. Sonst zöge das bloße Öffnen einer Bestandsfrage die beiden
        /// Textfelder still gleich.
        /// </para>
        /// </summary>
        public bool Beruehrt { get; private set; }

        /// <summary>
        /// Der Text der Zeile.
        /// <para>
        /// <b>Gezeigt wird <c>StepText</c>, denn der Beamer gewinnt</b>
        /// (<c>StepDisplayItem</c> nimmt <c>StepText</c> vor <c>Designation</c>, das Telefon
        /// umgekehrt). Beim Schreiben werden beide gesetzt - dann kann keine Frage mehr auf
        /// Beamer und Telefon verschiedene Antworten zeigen.
        /// </para>
        /// </summary>
        public string Text
        {
            get => Schritt.StepText;
            set
            {
                var neu = value ?? string.Empty;

                if (neu == Schritt.StepText && Beruehrt)
                    return;

                Schritt.StepText = neu;

                // Die Kopplung bleibt, solange niemand eine eigene Kurzform getippt hat.
                if (!kurzformBeruehrt)
                    Schritt.Designation = neu;

                Beruehrt = true;

                Melde();
                Melde(nameof(Beruehrt));
                Melde(nameof(IstLeer));
            }
        }

        /// <summary>Ob diese Zeile die richtige Antwort ist.</summary>
        public bool IstRichtig
        {
            get => Schritt.IsResult;
            set
            {
                if (Schritt.IsResult == value)
                    return;

                Schritt.IsResult = value;
                Beruehrt = true;

                Melde();
                Melde(nameof(Beruehrt));
            }
        }

        /// <summary>
        /// Der Buchstabe oder die Zahl, die im Spiel auf der Taste steht. <b>Nur Anzeige</b> -
        /// vergeben wird sie von <c>CalculateOrderdSteps</c>.
        /// </summary>
        public string Taste
        {
            get => taste;
            internal set
            {
                taste = value ?? string.Empty;
                Melde();
            }
        }

        private string taste = string.Empty;

        /// <summary>
        /// Ein abgeleiteter Zusatz neben der Zeile - etwa, was nach diesem Hinweis noch zu holen
        /// ist. <b>Nur Anzeige</b>, gefüllt von der Maske.
        /// </summary>
        public string Zusatz
        {
            get => zusatz;
            set
            {
                zusatz = value ?? string.Empty;
                Melde();
            }
        }

        private string zusatz = string.Empty;

        /// <summary>
        /// Ob die Einzelheiten dieser Zeile aufgeklappt sind.
        /// <para>
        /// <b>Nutzerwunsch vom 2026-09-06:</b> „generell wäre schön wenn man alles in einer maske
        /// steuert und nicht für details pro step in die andere maske muss". Bis hierher führte
        /// für Medium, Startschritt und Kurzform der Weg über ein zweites Fenster.
        /// </para>
        /// </summary>
        public bool IstOffen
        {
            get => istOffen;
            set
            {
                if (istOffen == value)
                    return;

                istOffen = value;

                Melde();
            }
        }

        private bool istOffen;

        /// <summary>
        /// Was am Telefon auf der Taste steht, wenn es etwas anderes sein soll als der Text am
        /// Beamer.
        /// <para>
        /// <b>Leer heißt: dasselbe wie der Text.</b> Das ist der Regelfall und bleibt es - der
        /// Beamer nimmt <c>StepText</c> vor <c>Designation</c>, das Telefon umgekehrt, und wer
        /// beide unterschiedlich füllt, bekommt zwei verschiedene Antworten, ohne dass etwas
        /// warnt. Genau deshalb schreibt <see cref="Text"/> weiterhin beide - <b>bis jemand hier
        /// wirklich etwas eintippt</b>.
        /// </para>
        /// <para>
        /// <b>Der Merker ist nötig, nicht bequem.</b> Würde „Kurzform gesetzt" aus dem Bestand
        /// abgeleitet (<c>Designation != StepText</c>), wäre die Kopplung auf jedem Bestandsschritt
        /// mit abweichender Bezeichnung sofort aufgehoben - eine Datenänderung beim bloßen Öffnen.
        /// Dieselbe Bauart wie <see cref="SchlageVor"/>, und aus demselben Anlass.
        /// </para>
        /// </summary>
        public string Kurzform
        {
            get => kurzformBeruehrt ? Schritt.Designation : string.Empty;
            set
            {
                var neu = value ?? string.Empty;

                if (neu.Length == 0)
                {
                    // Geleert heisst: wieder koppeln.
                    kurzformBeruehrt = false;
                    Schritt.Designation = Schritt.StepText;
                }
                else
                {
                    kurzformBeruehrt = true;
                    Schritt.Designation = neu;
                }

                Beruehrt = true;

                Melde();
                Melde(nameof(IstLeer));
            }
        }

        private bool kurzformBeruehrt;

        /// <summary>Was im Aufklappfeld über das Medium steht.</summary>
        public string Medienzeile
            => HatMedium
                ? $"{Mediumtext}: {Schritt.ResourceFileName}"
                : "Kein Medium an diesem Schritt.";

        /// <summary>Ob an dieser Zeile eine Mediendatei hängt.</summary>
        public bool HatMedium
            => Schritt.ResourceTyp != ResourceType.None
               && !string.IsNullOrWhiteSpace(Schritt.ResourceFileName);

        /// <summary>
        /// Was auf dem Medienknopf steht - <b>ein Wort, kein Zeichen</b>. Eine Büroklammer sagt
        /// nicht, ob schon etwas dranhängt und was es ist; „Bild" und „Ton" sagen beides.
        /// </summary>
        public string Mediumtext => Schritt.ResourceTyp switch
        {
            ResourceType.Image => "Bild",
            ResourceType.Video => "Video",
            ResourceType.Audio => "Ton",
            ResourceType.Document => "Datei",
            _ => "Medium …",
        };

        /// <summary>Ob die Zeile Text und Medium leer lässt - dann wird sie nicht geschrieben.</summary>
        public bool IstLeer
            => string.IsNullOrWhiteSpace(Schritt.StepText)
                && string.IsNullOrWhiteSpace(Schritt.Designation)
                && Schritt.ResourceTyp == ResourceType.None;

        /// <summary>
        /// Setzt einen abgeleiteten Vorschlagstext, ohne die Zeile als berührt zu gelten.
        /// <para>
        /// <b>Überschrieben wird nur, was leer ist oder was der eigene letzte Vorschlag war.</b>
        /// Das ist keine Feinheit: ohne diese Bedingung ersetzte das bloße Öffnen einer
        /// bestehenden Schätzfrage die vom Spielleiter getippte Auflösung durch den abgeleiteten
        /// Text - beim ersten Rundlauf gemessen, „3798 Meter" wurde zu „0".
        /// </para>
        /// <para>
        /// So folgt der Text dem Sollwert, solange er ihm gefolgt ist, und hört damit auf, sobald
        /// jemand etwas Eigenes hineinschreibt.
        /// </para>
        /// </summary>
        public void SchlageVor(string text)
        {
            if (Beruehrt)
                return;

            var jetzt = Schritt.StepText ?? string.Empty;

            if (jetzt.Length > 0 && jetzt != letzterVorschlag)
                return;

            letzterVorschlag = text ?? string.Empty;

            Schritt.StepText = letzterVorschlag;
            Schritt.Designation = letzterVorschlag;

            Melde(nameof(Text));
            Melde(nameof(IstLeer));
        }

        private string? letzterVorschlag;

        /// <summary>
        /// Meldet alle Felder neu. <b>Nötig, nachdem der Schritt-Dialog daran war</b> - er
        /// verändert denselben Schritt, meldet aber nichts an diese Zeile.
        /// </summary>
        public void MeldeAlles()
        {
            Melde(nameof(Text));
            Melde(nameof(IstRichtig));
            Melde(nameof(IstLeer));
            Melde(nameof(HatMedium));
            Melde(nameof(Mediumtext));
            Melde(nameof(Medienzeile));
            Melde(nameof(Kurzform));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Melde([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
