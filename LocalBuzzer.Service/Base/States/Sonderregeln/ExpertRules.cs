using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;

namespace LocalBuzzer.Service.Base.States.Sonderregeln
{
    /// <summary>Vorphase Experte (M35 C3b): jeder waehlt, wer fuer alle antwortet - nie sich selbst, nur Mitspieler.</summary>
    public sealed class ExpertRules : SpecialRuleChoiceRules
    {
        public ExpertRules(MinigameRulesContext kontext)
            : base(kontext)
        {
        }

        public override CellSpecialRule Rule => CellSpecialRule.Expert;

        protected override bool SelfAllowed => false;

        protected override string Prompt => "Wer soll für alle antworten? Du kannst nicht dich selbst wählen.";
    }
}
