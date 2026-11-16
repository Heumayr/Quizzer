using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon bei der Stoppuhr abgibt. Feldnamen = <c>stoppuhr.js</c>.</summary>
    public sealed class StopwatchPayload
    {
        /// <summary>Gemessen am Telefon vom Start bis zum Stopp.</summary>
        public int? Milliseconds { get; set; }
    }

    /// <summary>Was ein Telefon bei der Stoppuhr sieht - nur seine eigenen Werte, am Ende die Sieger.</summary>
    public sealed class StopwatchClientInfo
    {
        /// <summary>Die Zielzeit in Sekunden.</summary>
        public int TargetSeconds { get; set; }

        /// <summary>So lange ist die Uhr zu sehen.</summary>
        public int VisibleMs { get; set; }

        /// <summary>Darueber keine Wertung - das Telefon gibt dann selbst auf.</summary>
        public int MaxMs { get; set; }

        public int? OwnMilliseconds { get; set; }

        /// <summary>Der eigene Abstand zur Zielzeit; <c>null</c> ohne gewertete Zeit.</summary>
        public int? OwnDistanceMs { get; set; }

        /// <summary>Erst am Ende: ob dieser Spieler gewonnen hat.</summary>
        public bool IsWinner { get; set; }

        public List<string> WinnerNames { get; set; } = new();

        public int? WinnerDistanceMs { get; set; }
    }

    /// <summary>
    /// Die Stoppuhr am Server (M35 C1): prueft die gemeldete Zeit (<see cref="StoppuhrEvaluator.Classify"/>)
    /// und haelt die Runde fuer die Wertung. Gestartet wird am Telefon, gemessen auch.
    /// </summary>
    public sealed class StopwatchRules : IMinigameRules
    {
        private readonly int ziel;
        private readonly Func<Guid, string?> nameVon;
        private Dictionary<Guid, StoppuhrVersuch> laufend = new();
        private Dictionary<Guid, StoppuhrVersuch> gezaehlt = new();

        /// <param name="zielSekunden">Bei dieser Zeit soll gestoppt werden.</param>
        /// <param name="nameVon">Anzeigename zu einer Spieler-Id, fuer die Sieger am Ende.</param>
        public StopwatchRules(int zielSekunden, Func<Guid, string?> nameVon)
        {
            ziel = zielSekunden;
            this.nameVon = nameVon;
        }

        public MinigameKind Kind => MinigameKind.Stopwatch;

        /// <summary>Die Versuche der gezaehlten Runde.</summary>
        public IReadOnlyDictionary<Guid, StoppuhrVersuch> Gezaehlt => gezaehlt;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => laufend.ToDictionary(e => e.Key, e => (object)e.Value);

        public void StartRound(int round)
        {
            laufend = new();
            gezaehlt = new();
        }

        public object? Accept(Player player, JsonElement payload)
        {
            var versuch = MinigamePayload.Read<StopwatchPayload>(payload) is { } abgabe
                ? StoppuhrEvaluator.Classify(abgabe.Milliseconds, ziel)
                : null;

            if (versuch != null)
                laufend[player.Id] = versuch;

            return versuch;
        }

        public bool HasSubmitted(Guid playerId) => laufend.ContainsKey(playerId);

        public void CloseRound() => gezaehlt = new(laufend);

        public void DiscardRunning() => laufend = new();

        public void Clear()
        {
            laufend = new();
            gezaehlt = new();
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var info = new StopwatchClientInfo
            {
                TargetSeconds = ziel,
                VisibleMs = StoppuhrEvaluator.VisibleMs,
                MaxMs = StoppuhrEvaluator.MaxMs(ziel),
            };

            if (player == null)
                return info;

            var eigene = laufend.GetValueOrDefault(player.Id) ?? gezaehlt.GetValueOrDefault(player.Id);

            if (eigene != null)
            {
                info.OwnMilliseconds = eigene.Millisekunden;
                info.OwnDistanceMs = eigene.Gewertet ? Math.Abs(eigene.Millisekunden - ziel * 1000) : null;
            }

            if (phase == MinigamePhase.Finished)
            {
                var ergebnis = StoppuhrEvaluator.Werte(gezaehlt, ziel);
                var gewinner = ergebnis.Rangfolge.WinnerIds;

                info.IsWinner = ergebnis.IstGewinner(player.Id);
                info.WinnerNames = gewinner.Select(id => nameVon(id) ?? "?").OrderBy(n => n).ToList();
                info.WinnerDistanceMs = gewinner.Count > 0 ? ergebnis.AbstandVon(gewinner[0]) : null;
            }

            return info;
        }
    }
}
