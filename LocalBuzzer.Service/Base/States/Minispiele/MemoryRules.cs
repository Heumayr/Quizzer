using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon beim Memory abgibt: welche Karte es umdreht. Feldname = <c>memory.js</c>.</summary>
    public sealed class MemoryPayload
    {
        /// <summary>Die Karte, ab 0; fehlt sie, ist die Abgabe kaputt.</summary>
        public int? Card { get; set; }
    }

    /// <summary>Eine Karte, die das Telefon sehen darf - offen oder gefunden. Feldnamen = <c>memory.js</c> (<c>karte</c>).</summary>
    public sealed class MemoryCardInfo
    {
        public int Index { get; set; }

        public string Symbol { get; set; } = string.Empty;

        /// <summary>Fuer Vorleser: „Hund".</summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Was ein Telefon beim Memory sieht - nie die Belegung, nur offene Karten und eigene Paare.</summary>
    public sealed class MemoryClientInfo
    {
        public int CardCount { get; set; }

        public int TotalPairs { get; set; }

        /// <summary>So viel Zeit bleibt an der Serveruhr, gemessen beim Versand; <c>null</c> ausserhalb der Runde.</summary>
        public int? RemainingMs { get; set; }

        /// <summary>Die gerade offenen Karten dieses Telefons - eine, oder zwei, die kein Paar sind. Nur waehrend der Runde.</summary>
        public List<MemoryCardInfo> OpenCards { get; set; } = new();

        /// <summary>Die Karten seiner gefundenen Paare.</summary>
        public List<MemoryCardInfo> FoundCards { get; set; } = new();

        public int OwnPairs { get; set; }

        /// <summary>Wann er fertig war, an der Serveruhr seit dem Start; <c>null</c>, solange nicht.</summary>
        public int? OwnFinishedMs { get; set; }

        /// <summary>Erst am Ende: ob dieser Spieler gewonnen hat.</summary>
        public bool IsWinner { get; set; }

        public List<string> WinnerNames { get; set; } = new();
    }

    /// <summary>
    /// Das Memory am Server (M35 C5): die Belegung wird je Runde gemischt und bleibt hier; jedes Umdrehen
    /// fragt den Server, und das Telefon erfaehrt nur die Karte, die es gerade umdreht, und seine Paare.
    /// <para>
    /// <b>Zwei Karten, die kein Paar sind, schliessen beim naechsten Umdrehen</b>, nicht nach einer Wartezeit:
    /// so bleibt der Server rein antwortend (kein Takt je Telefon, kein Versand ohne Anfrage), und wer
    /// sich die Karten laenger ansieht, zahlt mit seiner eigenen Zeit. Fertig ist, wer alle Paare hat,
    /// gemessen an der Serveruhr beim Eintreffen des letzten Umdrehens; sonst schliesst die Fristwache.
    /// </para>
    /// </summary>
    public sealed class MemoryRules : IMinigameDeadlineRules
    {
        private readonly MinigameRulesContext kontext;
        private readonly TimeSpan vorlauf;
        private readonly TimeSpan dauer;
        private readonly int paare;
        private int[] belegung = [];
        private TimeSpan start;
        private bool laeuft;
        private Dictionary<Guid, Feld> laufend = new();
        private Dictionary<Guid, MemoryStand> gezaehlt = new();

        public MemoryRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            this.kontext = kontext;
            vorlauf = TimeSpan.FromSeconds(Math.Max(0, setup.CountdownSeconds));
            dauer = TimeSpan.FromSeconds(Math.Max(1, setup.DurationSeconds));
            paare = MemoryBelegung.PaareFuer(setup.Variant);
        }

        public MinigameKind Kind => MinigameKind.Memory;

        public int PaareGesamt => paare;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => laufend.ToDictionary(f => f.Key, f => (object)f.Value.Stand);

        /// <summary>Seit wann gespielt wird; im Vorlauf negativ.</summary>
        private TimeSpan Laufzeit => kontext.Jetzt() - start;

        public bool IsDue => laeuft && Laufzeit >= dauer;

        public void StartRound(int round)
        {
            belegung = MemoryBelegung.Mische(paare, kontext.Zufall);
            start = kontext.Jetzt() + vorlauf;
            laufend = new();
            gezaehlt = new();
            laeuft = true;
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (MinigamePayload.Read<MemoryPayload>(payload)?.Card is not { } karte || karte < 0 || karte >= belegung.Length)
                return null;

            var jetzt = kontext.Jetzt();
            var zeit = jetzt - start;

            // Im Vorlauf liegt noch kein Feld; ab der Frist zaehlt nichts mehr, auch vor dem Schliessen.
            if (!laeuft || zeit < TimeSpan.Zero || zeit >= dauer)
                return null;

            if (!laufend.TryGetValue(player.Id, out var feld))
                laufend[player.Id] = feld = new Feld();

            if (!feld.DarfUmdrehen(karte) || !feld.Takt.TryTake(jetzt))
                return null;

            var paar = feld.Drehe(karte, belegung);

            if (feld.Gefunden.Count == belegung.Length)
                feld.Fertig = zeit;

            return new MemoryZug(karte, paar, feld.Stand);
        }

        /// <summary>Abgegeben hat, wer alle Paare hat - dann ist er fuer diese Runde gesperrt.</summary>
        public bool HasSubmitted(Guid playerId) => laufend.TryGetValue(playerId, out var feld) && feld.Fertig != null;

        public void CloseRound()
        {
            gezaehlt = laufend.ToDictionary(f => f.Key, f => f.Value.Stand);
            laeuft = false;
        }

        /// <summary>Verwirft nur eine laufende Runde - danach bleiben die gefundenen Karten liegen (M37 P6, P2-1, wie V25).</summary>
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
            belegung = [];
            laeuft = false;
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var info = new MemoryClientInfo { CardCount = paare * 2, TotalPairs = paare };

            if (player == null)
                return info;

            var feld = laufend.GetValueOrDefault(player.Id);
            var stand = feld?.Stand ?? gezaehlt.GetValueOrDefault(player.Id);

            info.OwnPairs = stand?.Paare ?? 0;
            info.OwnFinishedMs = stand?.Fertig is { } fertig ? (int)fertig.TotalMilliseconds : null;
            info.FoundCards = feld?.Gefunden.Order().Select(Karte).ToList() ?? new();

            if (phase == MinigamePhase.Running && laeuft)
            {
                info.OpenCards = feld?.Offen.Select(Karte).ToList() ?? new();
                info.RemainingMs = (int)Math.Clamp((dauer - Laufzeit).TotalMilliseconds, 0, dauer.TotalMilliseconds);
            }

            if (phase == MinigamePhase.Finished)
            {
                var gewinner = MemoryEvaluator.Gewinner(gezaehlt);
                info.IsWinner = gewinner.Contains(player.Id);
                info.WinnerNames = gewinner.Select(id => kontext.NameVon(id) ?? "?").OrderBy(n => n).ToList();
            }

            return info;
        }

        /// <summary>Genau eine Karte mit ihrem Motiv - nur fuer Karten, die dieses Telefon sehen darf.</summary>
        private MemoryCardInfo Karte(int index)
        {
            var motiv = MemoryBelegung.Motive[belegung[index]];
            return new MemoryCardInfo { Index = index, Symbol = motiv.Symbol, Name = motiv.Name };
        }

        /// <summary>Das Feld eines Telefons in der laufenden Runde.</summary>
        private sealed class Feld
        {
            public List<int> Offen { get; } = new();

            public HashSet<int> Gefunden { get; } = new();

            public TimeSpan? Fertig { get; set; }

            public SlidingRateLimit Takt { get; } = new(MemoryEvaluator.MaxUmdrehenJeSekunde, TimeSpan.FromSeconds(1));

            public MemoryStand Stand => new(Gefunden.Count / 2, Fertig);

            /// <summary>Nicht nach dem Ende, keine gefundene und nicht die eine, die schon offen liegt.</summary>
            public bool DarfUmdrehen(int karte)
                => Fertig == null && !Gefunden.Contains(karte) && !(Offen.Count == 1 && Offen[0] == karte);

            /// <summary>Dreht um; zwei offene ohne Paar schliessen dabei zuerst. <c>true</c>, wenn ein Paar gefunden ist.</summary>
            public bool Drehe(int karte, int[] belegung)
            {
                if (Offen.Count == 2)
                    Offen.Clear();

                Offen.Add(karte);

                if (Offen.Count < 2 || belegung[Offen[0]] != belegung[Offen[1]])
                    return false;

                Gefunden.UnionWith(Offen);
                Offen.Clear();
                return true;
            }
        }
    }
}
