using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;

namespace Quizzer.DataModels.Questions.Schrittbau
{
    /// <summary>
    /// Wahr oder falsch: die Zeilen sind <b>Aussagen</b>, der Haken heißt „wahr".
    /// <para>
    /// <b>Die Zahl der wählbaren Tasten ist die Zahl der Aussagen</b> - jeder darf jede für wahr
    /// halten. Abgeleitet wie bei Multiple Choice, nicht eingegeben.
    /// </para>
    /// </summary>
    public sealed class WahrFalschComposer : StepComposerBase
    {
        /// <summary>So viele Aussagen stehen von Anfang an da.</summary>
        public const int Vorgabezeilen = 4;

        public override QuestionType Typ => QuestionType.TrueFalse;

        public override string ZeilenTitel => "Aussagen (Haken = wahr)";

        public override string AbschlussTitel => "Was am Ende zusätzlich steht (optional)";

        protected override int Grundzeilen => Vorgabezeilen;

        public override void Schreib(QuestionBase frage, Schrittbild bild)
        {
            base.Schreib(frage, bild);

            frage.BuzzerMaxAllowedKeySelect = Math.Max(bild.Zeilen.Count(z => !z.IstLeer), 1);
        }
    }
}
