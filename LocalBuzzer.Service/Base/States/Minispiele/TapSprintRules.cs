using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon im Tippsprint meldet - etwa alle 0,5 s und am Schluss. Feldnamen = <c>tippsprint.js</c>.</summary>
    public sealed class TapSprintPayload
    {
        public int Taps { get; set; }

        /// <summary>So lange laeuft der Sprint, gemessen am Telefon.</summary>
        public int ElapsedMs { get; set; }

        /// <summary>Der Endstand - danach nimmt der Server nichts mehr an.</summary>
        public bool IsFinal { get; set; }
    }

    /// <summary>Was ein Telefon im Tippsprint sieht - nur seine eigenen Werte, am Ende die Sieger.</summary>
    public sealed class TapSprintClientInfo
    {
        public int DurationSeconds { get; set; }

        /// <summary>Dieselbe Grenze wie am Server - das Telefon zaehlt nicht mehr, als angenommen wird.</summary>
        public int MaxTapsPerSecond { get; set; }

        public int ReportIntervalMs { get; set; }

        /// <summary>Der zuletzt angenommene eigene Stand - nach dem Wiederverbinden geht es dort weiter.</summary>
        public int? OwnTaps { get; set; }

        public int? OwnElapsedMs { get; set; }

        /// <summary>Die Runde ist zu, und der eigene Endstand kam nie an - der Stand zaehlt nicht (M37 P10, P8-4).</summary>
        public bool OwnFinalMissing { get; set; }

        /// <summary>Erst am Ende: ob dieser Spieler gewonnen hat.</summary>
        public bool IsWinner { get; set; }

        public List<string> WinnerNames { get; set; } = new();

        public int? WinnerTaps { get; set; }
    }

    /// <summary>Liest die Nutzlast einer Art; kaputt heisst <c>null</c> - sie kommt aus dem WLAN.</summary>
    internal static class MinigamePayload
    {
        private static readonly JsonSerializerOptions Lesen = new(JsonSerializerDefaults.Web);

        public static T? Read<T>(JsonElement payload) where T : class
        {
            if (payload.ValueKind != JsonValueKind.Object)
                return null;

            try
            {
                return payload.Deserialize<T>(Lesen);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Der Tippsprint am Server (M35 C1): nimmt Zwischen- und Endstaende an, wenn sie plausibel sind
    /// (<see cref="TippsprintEvaluator.Pruefe"/>). <b>Abgegeben hat erst, wer den Endstand geschickt
    /// hat</b> - die Zwischenstaende laufen am Rahmen vorbei, der nur eine Abgabe je Runde kennt.
    /// </summary>
    public sealed class TapSprintRules : IMinigameRules
    {
        private readonly int dauer;
        private readonly Func<Guid, string?> nameVon;
        private Dictionary<Guid, TippsprintStand> laufend = new();
        private Dictionary<Guid, int> gezaehlt = new();
        private bool laeuft;

        /// <param name="dauerSekunden">So lange wird getippt.</param>
        /// <param name="nameVon">Anzeigename zu einer Spieler-Id, fuer die Sieger am Ende.</param>
        public TapSprintRules(int dauerSekunden, Func<Guid, string?> nameVon)
        {
            dauer = dauerSekunden;
            this.nameVon = nameVon;
        }

        public MinigameKind Kind => MinigameKind.TapSprint;

        /// <summary>Die Endstaende der gezaehlten Runde.</summary>
        public IReadOnlyDictionary<Guid, int> Endstaende => gezaehlt;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => laufend.Where(e => e.Value.Ende).ToDictionary(e => e.Key, e => (object)e.Value);

        public void StartRound(int round)
        {
            // Eine Wiederholung ersetzt die Runde - die alte zaehlt nicht mehr.
            laufend = new();
            gezaehlt = new();
            laeuft = true;
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (MinigamePayload.Read<TapSprintPayload>(payload) is not { } abgabe)
                return null;

            var stand = TippsprintEvaluator.Pruefe(
                laufend.GetValueOrDefault(player.Id), new(abgabe.Taps, abgabe.ElapsedMs, abgabe.IsFinal), dauer);

            if (stand != null)
                laufend[player.Id] = stand;

            return stand;
        }

        public bool HasSubmitted(Guid playerId) => laufend.TryGetValue(playerId, out var stand) && stand.Ende;

        public void CloseRound()
        {
            gezaehlt = laufend.Where(e => e.Value.Ende).ToDictionary(e => e.Key, e => e.Value.Tipps);
            laeuft = false;
        }

        /// <summary>Verwirft nur eine laufende Runde - danach bleibt der Stand eines Telefons ohne Endstand stehen (M37 P8, P6-7, wie P2-1).</summary>
        public void DiscardRunning()
        {
            if (!laeuft)
                return;

            laufend = new();
            laeuft = false;
        }

        public void Clear()
        {
            laufend = new();
            gezaehlt = new();
            laeuft = false;
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var info = new TapSprintClientInfo
            {
                DurationSeconds = dauer,
                MaxTapsPerSecond = TippsprintEvaluator.MaxTapsPerSecond,
                ReportIntervalMs = TippsprintEvaluator.ReportIntervalMs,
            };

            if (player == null)
                return info;

            if (laufend.TryGetValue(player.Id, out var stand))
            {
                info.OwnTaps = stand.Tipps;
                info.OwnElapsedMs = stand.Millisekunden;
                info.OwnFinalMissing = !laeuft && !stand.Ende;
            }
            else if (gezaehlt.TryGetValue(player.Id, out var tipps))
            {
                info.OwnTaps = tipps;
            }

            if (phase == MinigamePhase.Finished)
            {
                var ergebnis = TippsprintEvaluator.Werte(gezaehlt);
                info.IsWinner = ergebnis.IstGewinner(player.Id);
                info.WinnerNames = ergebnis.Gewinner.Select(id => nameVon(id) ?? "?").OrderBy(n => n).ToList();
                info.WinnerTaps = ergebnis.Gewinner.Count > 0 ? gezaehlt[ergebnis.Gewinner.First()] : null;
            }

            return info;
        }
    }
}
