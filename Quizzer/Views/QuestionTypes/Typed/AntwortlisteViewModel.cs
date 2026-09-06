using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions.Schrittbau;

namespace Quizzer.Views.QuestionTypes.Typed
{
    /// <summary>
    /// Multiple Choice: die Schritte sind <b>Antwortmöglichkeiten</b>.
    /// <para>
    /// <b>Kein Verschieben.</b> Das Spiel mischt die Antworten bei jedem Spielen neu
    /// (<c>UseRandomSequenceOnNoneFinishSteps</c>) - ein Pfeil versprächen eine Reihenfolge, die
    /// es nicht gibt.
    /// </para>
    /// </summary>
    public sealed class AntwortlisteViewModel : ZeileneditorViewModel
    {
        private readonly QuestionBase frage;
        private readonly int urspruenglicheTastenzahl;

        internal AntwortlisteViewModel(IStepComposer composer, QuestionBase frage, Action melde)
            : base(composer, frage, melde)
        {
            this.frage = frage;

            urspruenglicheTastenzahl = frage.BuzzerMaxAllowedKeySelect;

            LeiteTastenzahlAb();
        }

        /// <summary>
        /// Setzt die Zahl der wählbaren Antworten auf die Zahl der Häkchen - <b>sofort, nicht
        /// erst beim Speichern</b>.
        /// <para>
        /// <b>Sonst sperrt die Prüfung das Speichern der Behebung.</b>
        /// <c>KeySelectCountMismatch</c> ist ein Fehler, und eine Bestandsfrage mit einem Häkchen
        /// und gespeicherter 3 trüge ihn, bis der Wert stimmt - der aber erst beim Speichern
        /// gesetzt würde, das die Prüfung gerade verhindert.
        /// </para>
        /// </summary>
        private void LeiteTastenzahlAb()
            => frage.BuzzerMaxAllowedKeySelect = Math.Max(Richtige, 1);

        private int Richtige => Zeilen.Count(z => !z.IstLeer && z.IstRichtig);

        /// <summary>
        /// Sagt es, wenn dabei ein gespeicherter Wert überschrieben wurde.
        /// <para>
        /// <b>Sachlich behebt das einen Defekt</b> - mit abweichendem Wert war die Frage nie
        /// richtig beantwortbar. Angewiesen hat es trotzdem niemand, also muss es dastehen und
        /// nicht hinterher auffallen.
        /// </para>
        /// </summary>
        public string Korrekturhinweis
            => urspruenglicheTastenzahl != frage.BuzzerMaxAllowedKeySelect
                ? $" Wählbare Antworten war auf {urspruenglicheTastenzahl} gespeichert und "
                  + $"wird beim Speichern auf {frage.BuzzerMaxAllowedKeySelect} gesetzt."
                : string.Empty;

        public override bool DarfSortieren => false;

        /// <summary>
        /// <b>Ein Klick, nicht zwei.</b>
        /// <para>
        /// <b>Nutzerwunsch vom 2026-09-06:</b> „auch das setzten der richtigen antwort muss ein
        /// klick sein". Das <i>Setzen</i> war schon einer - das <i>Wechseln</i> kostete zwei,
        /// und der Zustand dazwischen war nicht bloß umständlich, sondern falsch: mit zwei
        /// Häkchen verlangt die Frage am Telefon zwei Tastendrücke.
        /// </para>
        /// <para>
        /// <b>Mehrere richtige Antworten bleiben möglich</b> - der Rückweg steht im Hinweis, in
        /// dem Augenblick, in dem er entsteht. Ein Modus wäre etwas, das man finden, verstehen
        /// und behalten muss; hier gibt es nichts zu finden.
        /// </para>
        /// </summary>
        protected override bool ErzwingtEinzelloesung => true;

        public override string Tastenerklaerung
            => "Die Reihenfolge im Editor. Am Telefon werden die Antworten bei jedem Spielen neu "
               + "gemischt - der Buchstabe dort ist ein anderer.";

        /// <summary>Ob gerade ein Rückweg zu mehreren Lösungen angeboten wird.</summary>
        public System.Windows.Visibility RueckwegVisibility
            => ZuletztGeraeumt == null
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;

        /// <summary>Die Beschriftung des Rückweg-Knopfs, mit dem Text der geräumten Zeile.</summary>
        public string Rueckwegtext
            => ZuletztGeraeumt == null
                ? string.Empty
                : $"„{ZuletztGeraeumt.Text}\" auch richtig lassen";

        /// <summary>Was unter der Antwortliste steht.</summary>
        public override string Zeilenhinweis
            => Tastenzeile
               + " Die Antworten werden bei jedem Spielen neu gemischt - der Buchstabe hier ist "
               + "die Reihenfolge im Editor, nicht die auf dem Telefon."
               + Korrekturhinweis;

        /// <summary>
        /// Wie viele Tasten ein Spieler drücken darf - <b>abgeleitet, nicht eingegeben</b>.
        /// <para>
        /// Bisher stand daneben ein Zahlenfeld, das zur Zahl der Häkchen passen musste; tat es
        /// das nicht, war die Frage nie richtig beantwortbar und die Prüfung meldete
        /// <c>KeySelectCountMismatch</c>. Von dieser Maske aus ist der Fehler unerreichbar.
        /// </para>
        /// </summary>
        public string Tastenzeile
        {
            get
            {
                var richtige = Richtige;

                return richtige switch
                {
                    0 => "Noch keine Antwort als richtig markiert.",
                    1 => "1 Lösung - die Spieler wählen eine Taste.",
                    _ => $"{richtige} Lösungen - die Spieler wählen {richtige} Tasten.",
                };
            }
        }

        protected override void Melde()
        {
            LeiteTastenzahlAb();

            base.Melde();

            OnPropertyChanged(nameof(Tastenzeile));
            OnPropertyChanged(nameof(Korrekturhinweis));
            OnPropertyChanged(nameof(RueckwegVisibility));
            OnPropertyChanged(nameof(Rueckwegtext));
        }
    }
}
