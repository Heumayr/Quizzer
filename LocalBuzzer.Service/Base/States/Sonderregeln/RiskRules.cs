using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Sonderregeln
{
    /// <summary>Was das Telefon beim Risiko abgibt. Feldname = <c>risiko.js</c>.</summary>
    public sealed class RiskPayload
    {
        /// <summary><c>true</c> spielt doppelt, <c>false</c> einfach; fehlt es, ist die Abgabe kaputt.</summary>
        public bool? Doubled { get; set; }
    }

    /// <summary>Was ein Telefon in der Vorphase Risiko sieht.</summary>
    public sealed class RiskClientInfo
    {
        /// <summary>Seine Wahl, sobald abgegeben - auch nach dem Wiederverbinden.</summary>
        public bool? OwnDoubled { get; set; }
    }

    /// <summary>Vorphase Risiko (M35 C3): ja oder nein, „doppelt spielen".</summary>
    public sealed class RiskRules : SpecialRuleRules<bool>
    {
        public override CellSpecialRule Rule => CellSpecialRule.Risk;

        protected override bool? Read(Player player, JsonElement payload)
            => MinigamePayload.Read<RiskPayload>(payload)?.Doubled;

        protected override object? Info(Player? player, bool? eigene) => new RiskClientInfo { OwnDoubled = eigene };
    }
}
