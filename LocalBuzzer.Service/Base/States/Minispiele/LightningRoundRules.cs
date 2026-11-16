using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon in der Blitzrunde abgibt. Feldnamen = <c>blitzrunde.js</c>.</summary>
    public sealed class LightningRoundPayload
    {
        /// <summary>Die Nummer der Aussage, die das Telefon zeigte - eine andere als die laufende wird verworfen.</summary>
        public int StatementNumber { get; set; }

        /// <summary>Das Urteil: <c>true</c> = wahr; fehlt es, ist die Abgabe kaputt.</summary>
        public bool? Answer { get; set; }
    }

    /// <summary>Was ein Telefon in der Blitzrunde sieht - seine laufende Aussage, nie ihre Wahrheit.</summary>
    public sealed class LightningRoundClientInfo
    {
        public int DurationSeconds { get; set; }

        /// <summary>So viel Zeit bleibt an der Serveruhr, gemessen beim Versand; <c>null</c> ausserhalb der Runde.</summary>
        public int? RemainingMs { get; set; }

        public int TotalStatements { get; set; }

        /// <summary>Die laufende Aussage dieses Telefons, ab 1; 0 ausserhalb der Runde und wenn alle beantwortet sind.</summary>
        public int StatementNumber { get; set; }

        /// <summary>Der Text der laufenden Aussage - nur waehrend der Runde und nur fuer einen Spieler.</summary>
        public string? StatementText { get; set; }

        public int OwnAnswered { get; set; }

        /// <summary>Erst nach der Runde - waehrend ihr verriete der Zaehler die Wahrheit der letzten Aussage.</summary>
        public int? OwnCorrect { get; set; }

        /// <summary>Erst nach der Runde, aus demselben Grund.</summary>
        public int? OwnWrong { get; set; }
    }

    /// <summary>
    /// Die Blitzrunde am Server (M35 C5): die Aussagen samt Wahrheit bleiben hier, je Runde gemischt, fuer alle
    /// in derselben Reihenfolge. Jede Antwort bringt die naechste Aussage fuer genau dieses Telefon; ob sie
    /// stimmte, erfaehrt kein Telefon vor dem Ende der Runde. Fertig ist, wer alle beantwortet hat; sonst
    /// schliesst die Fristwache nach <c>DurationSeconds</c>.
    /// </summary>
    public sealed class LightningRoundRules : IMinigameDeadlineRules
    {
        private readonly MinigameRulesContext kontext;
        private readonly TimeSpan vorlauf;
        private readonly TimeSpan dauer;
        private readonly IReadOnlyList<MinispielZeile> aussagen;
        private int[] reihenfolge = [];
        private TimeSpan start;
        private bool laeuft;
        private Dictionary<Guid, Beurteiler> laufend = new();
        private Dictionary<Guid, BlitzrundeStand> gezaehlt = new();

        public LightningRoundRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            this.kontext = kontext;
            vorlauf = TimeSpan.FromSeconds(Math.Max(0, setup.CountdownSeconds));
            dauer = TimeSpan.FromSeconds(Math.Max(1, setup.DurationSeconds));
            aussagen = setup.Content ?? [];
        }

        public MinigameKind Kind => MinigameKind.LightningRound;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => laufend.ToDictionary(b => b.Key, b => (object)b.Value.Stand);

        /// <summary>Seit wann gespielt wird; im Vorlauf negativ.</summary>
        private TimeSpan Laufzeit => kontext.Jetzt() - start;

        public bool IsDue => laeuft && Laufzeit >= dauer;

        public void StartRound(int round)
        {
            reihenfolge = Mische(aussagen.Count, kontext.Zufall);
            start = kontext.Jetzt() + vorlauf;
            laufend = new();
            gezaehlt = new();
            laeuft = true;
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (MinigamePayload.Read<LightningRoundPayload>(payload) is not { Answer: { } urteil } abgabe)
                return null;

            var jetzt = kontext.Jetzt();
            var zeit = jetzt - start;

            // Im Vorlauf steht noch keine Aussage; ab der Frist zaehlt nichts mehr, auch vor dem Schliessen.
            if (!laeuft || zeit < TimeSpan.Zero || zeit >= dauer)
                return null;

            if (!laufend.TryGetValue(player.Id, out var spieler))
                laufend[player.Id] = spieler = new Beurteiler();

            // Eine alte Aussage (doppelt gesendet, nach dem Wiederverbinden) zaehlt nicht noch einmal.
            var nummer = spieler.Stand.Beantwortet + 1;
            if (abgabe.StatementNumber != nummer || nummer > aussagen.Count || !spieler.Takt.TryTake(jetzt))
                return null;

            spieler.Beantworte(urteil == aussagen[reihenfolge[nummer - 1]].Wahr);
            return new BlitzrundeFortschritt(nummer, nummer);
        }

        /// <summary>Abgegeben hat, wer alle Aussagen beurteilt hat - die Runde schliesst, wenn das fuer alle gilt.</summary>
        public bool HasSubmitted(Guid playerId)
            => aussagen.Count > 0 && laufend.TryGetValue(playerId, out var spieler) && spieler.Stand.Beantwortet >= aussagen.Count;

        public void CloseRound()
        {
            gezaehlt = laufend.ToDictionary(b => b.Key, b => b.Value.Stand);
            laeuft = false;
        }

        public void DiscardRunning()
        {
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
            var info = new LightningRoundClientInfo { DurationSeconds = (int)dauer.TotalSeconds, TotalStatements = aussagen.Count };

            if (player == null)
                return info;

            var stand = laufend.GetValueOrDefault(player.Id)?.Stand ?? gezaehlt.GetValueOrDefault(player.Id);
            info.OwnAnswered = stand?.Beantwortet ?? 0;

            if (phase == MinigamePhase.Running && laeuft)
            {
                var nummer = info.OwnAnswered + 1;

                if (nummer <= aussagen.Count)
                {
                    info.StatementNumber = nummer;
                    info.StatementText = aussagen[reihenfolge[nummer - 1]].Text;
                }

                info.RemainingMs = (int)Math.Clamp((dauer - Laufzeit).TotalMilliseconds, 0, dauer.TotalMilliseconds);
            }

            // Erst wenn keiner mehr antworten kann, darf das Telefon erfahren, was stimmte.
            if (!laeuft && phase is MinigamePhase.RoundOver or MinigamePhase.Finished)
            {
                info.OwnCorrect = stand?.Richtig ?? 0;
                info.OwnWrong = stand?.Falsch ?? 0;
            }

            return info;
        }

        /// <summary>Fisher-Yates ueber die Quelle des Rahmens - fuer alle dieselbe Reihenfolge.</summary>
        private static int[] Mische(int anzahl, Func<int, int, int> zufall)
        {
            var folge = Enumerable.Range(0, anzahl).ToArray();

            for (var i = anzahl - 1; i > 0; i--)
            {
                var j = Math.Clamp(zufall(0, i + 1), 0, i);
                (folge[i], folge[j]) = (folge[j], folge[i]);
            }

            return folge;
        }

        /// <summary>Der Stand eines Telefons in der laufenden Runde.</summary>
        private sealed class Beurteiler
        {
            public BlitzrundeStand Stand { get; private set; } = new(0, 0);

            public SlidingRateLimit Takt { get; } = new(BlitzrundeEvaluator.MaxAntwortenJeSekunde, TimeSpan.FromSeconds(1));

            public void Beantworte(bool richtig)
                => Stand = richtig ? Stand with { Richtig = Stand.Richtig + 1 } : Stand with { Falsch = Stand.Falsch + 1 };
        }
    }
}
