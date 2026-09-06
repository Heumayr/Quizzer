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

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Melde([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
