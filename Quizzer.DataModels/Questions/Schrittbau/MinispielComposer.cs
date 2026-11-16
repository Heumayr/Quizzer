using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;

namespace Quizzer.DataModels.Questions.Schrittbau
{
    /// <summary>
    /// Minispiel (M35 B2): die Zeilen sind die <b>Optionen</b>, aus denen gewaehlt wird - bei der
    /// Mehrheit die Antworten, spaeter Begriffe oder Aussagen. Kein Haken: richtig ist, was die
    /// Gruppe waehlt, nicht was markiert ist.
    /// </summary>
    public sealed class MinispielComposer : StepComposerBase
    {
        /// <summary>So viele Optionen stehen von Anfang an da.</summary>
        public const int Vorgabezeilen = 3;

        public override QuestionType Typ => QuestionType.Minigame;

        public override string ZeilenTitel => "Antwortoptionen";

        public override string AbschlussTitel => "Was am Ende steht (optional)";

        protected override int Grundzeilen => Vorgabezeilen;

        public override void Schreib(QuestionBase frage, Schrittbild bild)
        {
            base.Schreib(frage, bild);

            // Jeder waehlt genau eine Option - abgeleitet, nicht eingegeben.
            frage.BuzzerMaxAllowedKeySelect = 1;
        }
    }
}
