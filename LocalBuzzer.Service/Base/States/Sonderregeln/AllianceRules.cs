using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;

namespace LocalBuzzer.Service.Base.States.Sonderregeln
{
    /// <summary>
    /// Vorphase Allianz (M35 C3b): nur der Ausloeser waehlt, und zwar einen Partner - nie sich selbst, nur Mitspieler.
    /// Die anderen schauen zu. Ohne Ausloeser spielt das Fragefenster keine Vorphase; kommt sie doch, waehlt niemand.
    /// </summary>
    public sealed class AllianceRules : SpecialRuleChoiceRules
    {
        private readonly Guid? ausloeser;

        public AllianceRules(MinigameSetup setup, MinigameRulesContext kontext)
            : base(kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);

            ausloeser = setup.TriggerPlayerId is { } id && kontext.Mitspieler().Any(p => p.Id == id) ? id : null;
        }

        public override CellSpecialRule Rule => CellSpecialRule.Alliance;

        protected override bool SelfAllowed => false;

        protected override string Prompt => "Wähle deinen Partner – nur ihr zwei antwortet. Plus gibt es nur, wenn beide richtig sind.";

        public override bool Plays(Guid playerId) => playerId == ausloeser;

        public override string? WatcherText
            => ausloeser is { } id
                ? $"{Kontext.NameVon(id) ?? "?"} wählt einen Partner für die Allianz – du schaust zu."
                : "Niemand hat das Feld aufgedeckt – keine Allianz, der Spielleiter macht weiter.";
    }
}
