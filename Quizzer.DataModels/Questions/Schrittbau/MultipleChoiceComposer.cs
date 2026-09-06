using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;

namespace Quizzer.DataModels.Questions.Schrittbau
{
    /// <summary>
    /// Multiple Choice: die Schritte sind <b>Antwortmöglichkeiten</b>, nicht Hinweise.
    /// <para>
    /// <b>Die Zahl der wählbaren Antworten wird abgeleitet</b>, statt sie als eigenes Feld zu
    /// führen: sie ist die Zahl der Häkchen. Damit wird <c>KeySelectCountMismatch</c> von der
    /// Maske aus unerreichbar - der Fehler, bei dem eine Frage mit zwei richtigen Antworten nie
    /// richtig beantwortbar ist, weil die Höchstzahl auf 1 stehen blieb. Die Absicht des Profils
    /// bleibt gewahrt: zwei Häkchen ergeben 2.
    /// </para>
    /// </summary>
    public sealed class MultipleChoiceComposer : StepComposerBase
    {
        /// <summary>So viele Antwortzeilen stehen von Anfang an da.</summary>
        public const int Vorgabezeilen = 4;

        public override QuestionType Typ => QuestionType.MultipleChoice;

        public override string ZeilenTitel => "Antwortmöglichkeiten";

        public override string AbschlussTitel => "Was am Ende steht";

        protected override int Grundzeilen => Vorgabezeilen;

        public override void Schreib(QuestionBase frage, Schrittbild bild)
        {
            base.Schreib(frage, bild);

            frage.BuzzerMaxAllowedKeySelect = Math.Max(GewuenschteTastenzahl(bild), 1);
        }

        /// <summary>
        /// Wie viele Tasten ein Spieler drücken darf: so viele, wie richtig sind.
        /// <para>
        /// <b>Leere Zeilen zählen nicht mit</b> - sie werden auch nicht geschrieben, und ein
        /// Häkchen auf einer leeren Zeile wäre eine Antwort, die es im Spiel nicht gibt.
        /// </para>
        /// </summary>
        public static int GewuenschteTastenzahl(Schrittbild bild)
        {
            ArgumentNullException.ThrowIfNull(bild);

            return bild.Zeilen.Count(z => !z.IstLeer && z.IstRichtig);
        }
    }
}
