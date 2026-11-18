using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was ein Ratender beim Erklaerbaer abgibt. Feldname = <c>baer.js</c>.</summary>
    public sealed class ExplainItPayload
    {
        /// <summary>Was getippt wurde; leer ist die Abgabe kaputt.</summary>
        public string? Guess { get; set; }
    }

    /// <summary>Was ein Telefon beim Erklaerbaer sieht - den Begriff nur der Erklaerer, und nur waehrend der Runde.</summary>
    public sealed class ExplainItClientInfo
    {
        public int DurationSeconds { get; set; }

        /// <summary>So viel Zeit bleibt an der Serveruhr, gemessen beim Versand; <c>null</c> ausserhalb der Runde.</summary>
        public int? RemainingMs { get; set; }

        /// <summary>Wer erklaert - kein Geheimnis; <c>null</c>, solange der Spielleiter keinen gewaehlt hat.</summary>
        public string? ExplainerName { get; set; }

        /// <summary>Dieses Telefon erklaert - es raet nicht.</summary>
        public bool IsExplainer { get; set; }

        /// <summary>Der Begriff - nur fuer den Erklaerer und nur, solange die Runde laeuft.</summary>
        public string? Term { get; set; }

        /// <summary>Ob der letzte Versuch dieses Telefons in der laufenden Runde danebenlag.</summary>
        public bool LastWrong { get; set; }

        /// <summary>Erst nach der Runde: der Begriff, fuer alle.</summary>
        public string? Solution { get; set; }

        /// <summary>Erst nach der Runde: wer ihn erraten hat; <c>null</c>, wenn niemand.</summary>
        public string? SolverName { get; set; }

        /// <summary>Erst nach der Runde: ob dieses Telefon ihn erraten hat.</summary>
        public bool IsSolver { get; set; }
    }

    /// <summary>
    /// Der Erklaerbaer am Server (M35 C6): je Runde ein Begriff aus den Zeilen, in ihrer Reihenfolge; eine Wiederholung
    /// zieht den naechsten, weil der alte schon erklaert ist. Den Begriff sieht nur der Erklaerer (der Ausloeser, sonst
    /// waehlt ihn der Spielleiter, E1). Die anderen raten; die erste richtige Eingabe - unter der Sperre des Rahmens,
    /// also an der Serveruhr - schliesst die Runde. <b>Der Erklaerer kann nicht raten</b>, das prueft dieser Server.
    /// Verraten wertet der Spielleiter im Fragefenster; der Server weiss davon nichts.
    /// </summary>
    public sealed class ExplainItRules : IMinigamePairRules, IMinigameDeadlineRules
    {
        private readonly MinigameRulesContext kontext;
        private readonly TimeSpan vorlauf;
        private readonly TimeSpan dauer;
        private readonly IReadOnlyList<string> begriffe;
        private Guid? erklaerer;
        private int gezogen;
        private string? begriff;
        private Guid? rater;
        private TimeSpan start;
        private bool laeuft;
        private Dictionary<Guid, SlidingRateLimit> takt = new();
        private HashSet<Guid> daneben = new();

        public ExplainItRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            this.kontext = kontext;
            vorlauf = TimeSpan.FromSeconds(Math.Max(0, setup.CountdownSeconds));
            dauer = TimeSpan.FromSeconds(Math.Max(1, setup.DurationSeconds));
            begriffe = (setup.Content ?? []).Select(z => z.Text).Where(t => t.Any(char.IsLetterOrDigit)).ToList();
            erklaerer = IstMitspieler(setup.TriggerPlayerId) ? setup.TriggerPlayerId : null;
        }

        public MinigameKind Kind => MinigameKind.ExplainIt;

        public Guid? ChooserId => erklaerer;

        /// <summary>Ein Gegenueber gibt es nicht.</summary>
        public Guid? PartnerId => null;

        /// <summary>Fuer den Spielleiter: der Begriff der laufenden oder letzten Runde - nie fuer ein Telefon.</summary>
        public string? CurrentTerm => begriff;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => rater is { } id && erklaerer is { } e
                ? new Dictionary<Guid, object> { [id] = new ErklaerbaerVersuch(true, e) }
                : new Dictionary<Guid, object>();

        private TimeSpan Laufzeit => kontext.Jetzt() - start;

        public bool IsDue => laeuft && Laufzeit >= dauer;

        public void StartRound(int round)
        {
            // Je Start ein neuer Begriff - auch bei einer Wiederholung; reichen sie nicht, beginnt die Liste von vorn.
            begriff = begriffe.Count > 0 ? begriffe[gezogen++ % begriffe.Count] : null;
            NeuerVersuch();
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (MinigamePayload.Read<ExplainItPayload>(payload) is not { Guess: { } eingabe } || string.IsNullOrWhiteSpace(eingabe))
                return null;

            var jetzt = kontext.Jetzt();
            var zeit = jetzt - start;

            // Der Erklaerer kennt den Begriff - sein Versuch zaehlt nie, auch wenn das Telefon ihn schickt.
            if (!laeuft || begriff == null || erklaerer is not { } e || player.Id == e || rater != null
                || zeit < TimeSpan.Zero || zeit >= dauer)
                return null;

            if (!takt.TryGetValue(player.Id, out var grenze))
                takt[player.Id] = grenze = new(ErklaerbaerEvaluator.MaxVersucheJeFenster, TimeSpan.FromSeconds(ErklaerbaerEvaluator.FensterSekunden));

            if (!grenze.TryTake(jetzt))
                return null;

            if (!BuchstabensalatBegriffe.Gleich(eingabe, begriff))
            {
                daneben.Add(player.Id);
                return new ErklaerbaerVersuch(false, e);
            }

            rater = player.Id;
            return new ErklaerbaerVersuch(true, e);
        }

        /// <summary>Ist der Begriff erraten, hat jeder abgegeben - dann schliesst die Runde.</summary>
        public bool HasSubmitted(Guid playerId) => rater != null;

        public void CloseRound() => laeuft = false;

        /// <summary>Verwirft nur eine laufende Runde - nach dem Treffer bleibt stehen, wer erraten hat (M37 P6, P2-1, wie V25).</summary>
        public void DiscardRunning()
        {
            if (!laeuft)
                return;

            rater = null;
            laeuft = false;
        }

        public void Clear()
        {
            gezogen = 0;
            begriff = null;
            rater = null;
            laeuft = false;
            takt = new();
            daneben = new();
        }

        /// <summary>
        /// Der Spielleiter legt den Erklaerer fest (E1); ein Gegenueber gibt es nicht. Mitten in der Runde kommt ein neuer
        /// Begriff - den alten kennt der bisherige Erklaerer, der jetzt mitraet.
        /// </summary>
        public bool SetPair(Guid chooserId, Guid? partnerId)
        {
            if (partnerId != null || !IstMitspieler(chooserId))
                return false;

            var wechsel = erklaerer != chooserId;
            erklaerer = chooserId;

            if (laeuft && wechsel && rater == null)
                StartRound(0);

            return true;
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var info = new ExplainItClientInfo
            {
                DurationSeconds = (int)dauer.TotalSeconds,
                ExplainerName = erklaerer is { } e ? kontext.NameVon(e) ?? "?" : null,
            };

            if (phase == MinigamePhase.Running && laeuft)
                info.RemainingMs = (int)Math.Clamp((dauer - Laufzeit).TotalMilliseconds, 0, dauer.TotalMilliseconds);

            // Erst wenn keiner mehr raten kann, ist der Begriff kein Geheimnis mehr.
            var aufgeloest = !laeuft && begriff != null && phase is MinigamePhase.RoundOver or MinigamePhase.Finished;
            if (aufgeloest)
            {
                info.Solution = begriff;
                info.SolverName = rater is { } r ? kontext.NameVon(r) ?? "?" : null;
            }

            if (player == null)
                return info;

            info.IsExplainer = player.Id == erklaerer;
            info.LastWrong = laeuft && daneben.Contains(player.Id);
            info.IsSolver = aufgeloest && player.Id == rater;

            if (info.IsExplainer && phase == MinigamePhase.Running && laeuft)
                info.Term = begriff;

            return info;
        }

        private void NeuerVersuch()
        {
            rater = null;
            start = kontext.Jetzt() + vorlauf;
            laeuft = true;
            takt = new();
            daneben = new();
        }

        private bool IstMitspieler(Guid? id) => id is { } gesucht && kontext.Mitspieler().Any(p => p.Id == gesucht);
    }
}
