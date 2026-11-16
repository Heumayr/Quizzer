using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Sonderregeln
{
    /// <summary>
    /// Die Vorphase einer Sonderregel am Server (M35 C3). Sie laeuft im Rahmen der Minispiele
    /// (<see cref="MinigameState"/>, eine Runde): der prueft Phase, Durchgang, Sperre und Doppelabgabe,
    /// hier wird nur die Abgabe selbst geprueft. Jeder gibt genau einmal ab; nach dem Schliessen bleibt
    /// die Abgabe stehen, damit ein wiederkommendes Telefon sie sieht.
    /// <para>Eine neue Regel (C3b) erbt hiervon und bringt Pruefung und Anzeige mit.</para>
    /// </summary>
    /// <typeparam name="T">Was ein Spieler abgibt - beim Einsatz die Zahl, beim Risiko ja/nein.</typeparam>
    public abstract class SpecialRuleRules<T> : IMinigameRules where T : struct
    {
        private Dictionary<Guid, T> laufend = new();

        /// <summary>Eine Sonderregel ist keine Minispiel-Art - die Art bleibt leer.</summary>
        public MinigameKind Kind => default;

        public abstract CellSpecialRule Rule { get; }

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => laufend.ToDictionary(e => e.Key, e => (object)e.Value);

        public void StartRound(int round) => laufend = new();

        public object? Accept(Player player, JsonElement payload)
        {
            if (Read(player, payload) is not { } wert)
                return null;

            laufend[player.Id] = wert;
            return wert;
        }

        public bool HasSubmitted(Guid playerId) => laufend.ContainsKey(playerId);

        public void CloseRound()
        {
        }

        public void DiscardRunning() => laufend = new();

        public void Clear() => laufend = new();

        public object? InfoFor(Player? player, MinigamePhase phase)
            => Info(player, player != null && laufend.TryGetValue(player.Id, out var eigene) ? eigene : null);

        /// <summary>Wer in der Vorphase abgibt - bei der Allianz nur der Ausloeser (M35 C3b).</summary>
        public virtual bool Plays(Guid playerId) => true;

        /// <summary>Was die Zuschauer der Vorphase lesen; <c>null</c>: der Rahmen sagt es allgemein.</summary>
        public virtual string? WatcherText => null;

        /// <summary>Prueft die Abgabe; <c>null</c> heisst verworfen.</summary>
        protected abstract T? Read(Player player, JsonElement payload);

        /// <summary>Was dieses Telefon sieht - samt seiner eigenen Abgabe, nie einer fremden.</summary>
        protected abstract object? Info(Player? player, T? eigene);
    }

    /// <summary>Die Vorphase zur Regel - die einzige Stelle am Server, die nach der Sonderregel verzweigt.</summary>
    internal static class SpecialRuleRulesFactory
    {
        /// <summary>Die Vorphase zur Regel; <c>null</c> bei einer unbekannten und bei einer ohne Vorphase (Kopfgeld).</summary>
        public static IMinigameRules? For(MinigameSetup setup, MinigameRulesContext kontext) => setup.SpecialRule switch
        {
            CellSpecialRule.Stake => new StakeRules(setup),
            CellSpecialRule.Risk => new RiskRules(),
            CellSpecialRule.Auction => new AuctionRules(setup),
            CellSpecialRule.Bet => new BetRules(setup, kontext),
            _ => null,
        };
    }
}
