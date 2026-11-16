using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon bei der Reaktion abgibt. Feldnamen = <c>reaktion.js</c>.</summary>
    public sealed class ReactionPayload
    {
        /// <summary>Vor Gruen getippt.</summary>
        public bool FalseStart { get; set; }

        /// <summary>Gemessen am Telefon vom Umschalten auf Gruen bis zum Tippen.</summary>
        public int? Milliseconds { get; set; }
    }

    /// <summary>Was ein Telefon bei der Reaktion sieht - nur seine eigenen Werte, am Ende die Sieger.</summary>
    public sealed class ReactionClientInfo
    {
        /// <summary>Nach so vielen Millisekunden schaltet das Telefon auf Gruen; 0 ausserhalb einer Runde.</summary>
        public int WaitMs { get; set; }

        /// <summary>Die eigene Abgabe in der laufenden bzw. zuletzt geschlossenen Runde.</summary>
        public ReactionOutcome? OwnOutcome { get; set; }

        public int? OwnMilliseconds { get; set; }

        /// <summary>Die eigene beste gueltige Zeit ueber die gespielten Runden.</summary>
        public int? BestMilliseconds { get; set; }

        /// <summary>Erst am Ende: ob dieser Spieler gewonnen hat.</summary>
        public bool IsWinner { get; set; }

        /// <summary>Erst am Ende: wer gewonnen hat und mit welcher Zeit.</summary>
        public List<string> WinnerNames { get; set; } = new();

        public int? WinnerMilliseconds { get; set; }
    }

    /// <summary>
    /// Die Reaktion am Server (M35 B3): wuerfelt je Runde die Wartezeit, prueft die gemeldete Zeit auf
    /// Plausibilitaet (<see cref="ReaktionEvaluator.Classify"/>) und haelt die Runden fuer die Wertung.
    /// </summary>
    public sealed class ReactionRules : IMinigameRules
    {
        private static readonly JsonSerializerOptions Lesen = new(JsonSerializerDefaults.Web);

        private readonly Func<int, int, int> zufall;
        private readonly Func<Guid, string?> nameVon;
        private readonly SortedDictionary<int, Dictionary<Guid, ReactionAttempt>> geschlossen = new();
        private Dictionary<Guid, ReactionAttempt> laufend = new();
        private int runde;

        /// <param name="zufall">Wie <see cref="Random.Next(int, int)"/> - eigene Quelle nur fuer Tests.</param>
        /// <param name="nameVon">Anzeigename zu einer Spieler-Id, fuer die Sieger am Ende.</param>
        public ReactionRules(Func<int, int, int> zufall, Func<Guid, string?> nameVon)
        {
            this.zufall = zufall;
            this.nameVon = nameVon;
        }

        public MinigameKind Kind => MinigameKind.Reaction;

        /// <summary>Die Wartezeit der laufenden Runde.</summary>
        public int WaitMs { get; private set; }

        /// <summary>Die gezaehlten Runden in ihrer Reihenfolge.</summary>
        public IReadOnlyList<IReadOnlyDictionary<Guid, ReactionAttempt>> ClosedRounds
            => geschlossen.Values.Select(r => (IReadOnlyDictionary<Guid, ReactionAttempt>)r).ToList();

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => laufend.ToDictionary(e => e.Key, e => (object)e.Value);

        public void StartRound(int round)
        {
            runde = round;
            laufend = new();

            // Eine Wiederholung ersetzt die Runde - die alte zaehlt nicht mehr.
            geschlossen.Remove(round);

            WaitMs = zufall(ReaktionEvaluator.MinWaitMs, ReaktionEvaluator.MaxWaitMs + 1);
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (payload.ValueKind != JsonValueKind.Object)
                return null;

            ReactionPayload? abgabe;

            try
            {
                abgabe = payload.Deserialize<ReactionPayload>(Lesen);
            }
            catch (JsonException)
            {
                // Kommt aus dem WLAN - eine kaputte Abgabe wird verworfen, nicht geworfen.
                return null;
            }

            var versuch = abgabe == null ? null : ReaktionEvaluator.Classify(abgabe.FalseStart, abgabe.Milliseconds);

            if (versuch != null)
                laufend[player.Id] = versuch;

            return versuch;
        }

        public bool HasSubmitted(Guid playerId) => laufend.ContainsKey(playerId);

        public void CloseRound() => geschlossen[runde] = laufend;

        public void DiscardRunning() => laufend = new();

        public void Clear()
        {
            geschlossen.Clear();
            laufend = new();
            runde = 0;
            WaitMs = 0;
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var info = new ReactionClientInfo { WaitMs = phase == MinigamePhase.Running ? WaitMs : 0 };

            if (player == null)
                return info;

            var eigene = phase == MinigamePhase.Running ? laufend : geschlossen.GetValueOrDefault(runde);

            if (eigene != null && eigene.TryGetValue(player.Id, out var versuch))
            {
                info.OwnOutcome = versuch.Outcome;
                info.OwnMilliseconds = versuch.Milliseconds;
            }

            var ergebnis = ReaktionEvaluator.Werte(ClosedRounds);
            info.BestMilliseconds = ergebnis.BestzeitVon(player.Id);

            if (phase == MinigamePhase.Finished)
            {
                info.IsWinner = ergebnis.IstGewinner(player.Id);
                info.WinnerNames = ergebnis.Gewinner.Select(id => nameVon(id) ?? "?").OrderBy(n => n).ToList();
                info.WinnerMilliseconds = ergebnis.Gewinner.Count > 0 ? ergebnis.Bestzeiten[ergebnis.Gewinner.First()] : null;
            }

            return info;
        }
    }
}
