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
        internal AntwortlisteViewModel(IStepComposer composer, QuestionBase frage, Action melde)
            : base(composer, frage, melde)
        {
        }

        public override bool DarfSortieren => false;

        /// <summary>Was unter der Antwortliste steht.</summary>
        public override string Zeilenhinweis
            => Tastenzeile + " Die Antworten werden bei jedem Spielen neu gemischt.";

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
                var richtige = Zeilen.Count(z => !z.IstLeer && z.IstRichtig);

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
            base.Melde();

            OnPropertyChanged(nameof(Tastenzeile));
        }
    }
}
