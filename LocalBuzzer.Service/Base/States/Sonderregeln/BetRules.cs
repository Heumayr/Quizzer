using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace LocalBuzzer.Service.Base.States.Sonderregeln
{
    /// <summary>
    /// Vorphase Wette (M35 C3b): jeder wettet auf einen Mitspieler, auch auf sich. Die Taste nennt den Gewinn - er
    /// folgt dem Rang beim Oeffnen und ist kein Geheimnis, die Kontostaende stehen ohnehin am Beamer.
    /// </summary>
    public sealed class BetRules : SpecialRuleChoiceRules
    {
        private readonly int einsatz;
        private readonly IReadOnlyDictionary<Guid, int> kontostaende;

        public BetRules(MinigameSetup setup, MinigameRulesContext kontext)
            : base(kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);

            einsatz = WetteRegel.Einsatz(setup.FieldPoints, setup.Share);
            kontostaende = setup.Balances ?? new Dictionary<Guid, int>();
        }

        public override CellSpecialRule Rule => CellSpecialRule.Bet;

        protected override bool SelfAllowed => true;

        protected override string Prompt => $"Auf wen wettest du? Einsatz {einsatz} – wer weiter hinten liegt, bringt mehr.";

        protected override string OptionName(Player spieler)
            => $"{base.OptionName(spieler)} – Gewinn {WetteRegel.Auf(spieler.Id, string.Empty, einsatz, kontostaende).Gewinn}";
    }
}
