using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Questions.Schrittbau
{
    /// <summary>
    /// Standardfrage: Hinweise und - erstmals - ein Feld für die richtige Antwort.
    /// <para>
    /// <b>Der Typ hat heute kein solches Feld.</b> <c>RequiresResultStep</c> steht auf
    /// <c>false</c>, <c>MinNormalSteps</c> auf 0, und die Prüfung fragt nie danach. Wer einen
    /// Abschluss will, legt ihn im Schritt-Dialog an und setzt dort ein Häkchen - sonst ergänzt
    /// das Modell einen leeren, und am Beamer steht am Ende nichts.
    /// </para>
    /// </summary>
    public class HinweisComposer : StepComposerBase
    {
        public override QuestionType Typ => QuestionType.Default;

        public override string ZeilenTitel => "Hinweise (optional)";

        public override string AbschlussTitel => "Die richtige Antwort";
    }

    /// <summary>
    /// Eigenschaftsfrage: dieselbe Form, aber die Hinweisleiter ist der Sinn der Sache und nicht
    /// optional (<c>MinNormalSteps = 1</c>). Mit jedem Hinweis sinken die erreichbaren Punkte.
    /// </summary>
    public sealed class EigenschaftComposer : HinweisComposer
    {
        public override QuestionType Typ => QuestionType.Properties;

        public override string ZeilenTitel => "Hinweise - je Hinweis sinken die Punkte";

        protected override int Grundzeilen => 3;
    }
}
