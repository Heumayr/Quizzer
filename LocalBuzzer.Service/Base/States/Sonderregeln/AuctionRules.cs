using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Sonderregeln;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Sonderregeln
{
    /// <summary>Was das Telefon bei der Auktion abgibt. Feldname = <c>einsatz.js</c> (die Auktion erbt das Modul).</summary>
    public sealed class AuctionPayload
    {
        /// <summary>Das Gebot; 0 heisst passen. Fehlt es oder ist es keine ganze Zahl, ist die Abgabe kaputt.</summary>
        public int? Amount { get; set; }
    }

    /// <summary>Was ein Telefon in der Auktion sieht - nur das eigene Gebot, nie ein fremdes.</summary>
    public sealed class AuctionClientInfo
    {
        /// <summary>So viel darf dieser Spieler hoechstens bieten.</summary>
        public int Max { get; set; }

        /// <summary>Sein Gebot, sobald abgegeben - auch nach dem Wiederverbinden.</summary>
        public int? OwnAmount { get; set; }
    }

    /// <summary>
    /// Vorphase Auktion (M35 C3b): geheimes Gebot 0 bis max(Kontostand, Feldpunkte), am Server geprueft. Jedes
    /// angenommene Gebot bekommt einen Eingang - der Rahmen nimmt Abgaben unter seiner Sperre an, also ist die
    /// Reihenfolge eindeutig; bei Gleichstand gewinnt das fruehere (<see cref="AuktionRegel.Ermittle"/>).
    /// </summary>
    public sealed class AuctionRules : SpecialRuleRules<Gebot>
    {
        private readonly int feldpunkte;
        private readonly IReadOnlyDictionary<Guid, int> kontostaende;
        private int eingang;

        public AuctionRules(MinigameSetup setup)
        {
            ArgumentNullException.ThrowIfNull(setup);

            feldpunkte = setup.FieldPoints;
            kontostaende = setup.Balances ?? new Dictionary<Guid, int>();
        }

        public override CellSpecialRule Rule => CellSpecialRule.Auction;

        /// <summary>Die Obergrenze dieses Spielers.</summary>
        public int Max(Guid playerId) => EinsatzRegel.Obergrenze(kontostaende.GetValueOrDefault(playerId), feldpunkte);

        protected override Gebot? Read(Player player, JsonElement payload)
        {
            if (MinigamePayload.Read<AuctionPayload>(payload)?.Amount is not { } betrag
                || !AuktionRegel.Zulaessig(betrag, kontostaende.GetValueOrDefault(player.Id), feldpunkte))
                return null;

            return new Gebot(betrag, ++eingang);
        }

        protected override object? Info(Player? player, Gebot? eigene)
            => new AuctionClientInfo { Max = player == null ? 0 : Max(player.Id), OwnAmount = eigene?.Betrag };
    }
}
