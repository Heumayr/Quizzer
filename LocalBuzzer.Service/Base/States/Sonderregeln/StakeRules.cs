using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Sonderregeln;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Sonderregeln
{
    /// <summary>Was das Telefon beim Einsatz abgibt. Feldname = <c>einsatz.js</c>.</summary>
    public sealed class StakePayload
    {
        /// <summary>Der Einsatz; fehlt er oder ist er keine ganze Zahl, ist die Abgabe kaputt.</summary>
        public int? Amount { get; set; }
    }

    /// <summary>Was ein Telefon in der Vorphase Einsatz sieht.</summary>
    public sealed class StakeClientInfo
    {
        /// <summary>So viel darf dieser Spieler hoechstens setzen.</summary>
        public int Max { get; set; }

        /// <summary>Sein Einsatz, sobald abgegeben - auch nach dem Wiederverbinden.</summary>
        public int? OwnAmount { get; set; }
    }

    /// <summary>
    /// Vorphase Einsatz (M35 C3): 0 bis max(Kontostand, Feldpunkte) je Spieler, am Server geprueft
    /// (<see cref="EinsatzRegel.Zulaessig"/>). Die Kontostaende bringt der Spielleiter beim Einstellen mit.
    /// <para>Das Finale (M37 E4) spielt dieselbe Vorphase - jeder setzt verdeckt, gesehen wird nur der eigene Einsatz.</para>
    /// </summary>
    public sealed class StakeRules : SpecialRuleRules<int>
    {
        private readonly int feldpunkte;
        private readonly IReadOnlyDictionary<Guid, int> kontostaende;

        public StakeRules(MinigameSetup setup)
        {
            ArgumentNullException.ThrowIfNull(setup);

            feldpunkte = setup.FieldPoints;
            kontostaende = setup.Balances ?? new Dictionary<Guid, int>();
            Rule = setup.SpecialRule == CellSpecialRule.Final ? CellSpecialRule.Final : CellSpecialRule.Stake;
        }

        public override CellSpecialRule Rule { get; }

        /// <summary>Die Obergrenze dieses Spielers.</summary>
        public int Max(Guid playerId) => EinsatzRegel.Obergrenze(kontostaende.GetValueOrDefault(playerId), feldpunkte);

        protected override int? Read(Player player, JsonElement payload)
        {
            if (MinigamePayload.Read<StakePayload>(payload)?.Amount is not { } einsatz)
                return null;

            return EinsatzRegel.Zulaessig(einsatz, kontostaende.GetValueOrDefault(player.Id), feldpunkte) ? einsatz : null;
        }

        protected override object? Info(Player? player, int? eigene)
            => new StakeClientInfo { Max = player == null ? 0 : Max(player.Id), OwnAmount = eigene };
    }
}
