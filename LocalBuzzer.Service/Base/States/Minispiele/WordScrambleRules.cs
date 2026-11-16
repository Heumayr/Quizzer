using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon beim Buchstabensalat abgibt. Feldnamen = <c>buchstabensalat.js</c>.</summary>
    public sealed class WordScramblePayload
    {
        /// <summary>Die Nummer des Begriffs, den das Telefon zeigte - eine andere als die laufende wird verworfen.</summary>
        public int TermNumber { get; set; }

        /// <summary>Was getippt wurde; leer ist die Abgabe kaputt.</summary>
        public string? Guess { get; set; }
    }

    /// <summary>Was ein Telefon beim Buchstabensalat sieht - das Wirrwarr des offenen Begriffs, nie den Begriff selbst.</summary>
    public sealed class WordScrambleClientInfo
    {
        public int DurationSeconds { get; set; }

        /// <summary>So viel Zeit bleibt an der Serveruhr, gemessen beim Versand; <c>null</c> ausserhalb der Runde.</summary>
        public int? RemainingMs { get; set; }

        public int TotalTerms { get; set; }

        /// <summary>Der offene Begriff, ab 1; 0 ausserhalb der Runde und wenn alle geloest sind.</summary>
        public int TermNumber { get; set; }

        /// <summary>„SÄKE" - die Buchstaben des offenen Begriffs, gemischt; fuer alle dasselbe.</summary>
        public string? Scramble { get; set; }

        public int OwnSolved { get; set; }

        /// <summary>Ob der letzte Versuch dieses Telefons am offenen Begriff danebenlag.</summary>
        public bool LastWrong { get; set; }

        /// <summary>Der zuletzt geloeste Begriff - er ist nicht mehr offen; <c>null</c>, solange keiner geloest ist.</summary>
        public string? LastSolved { get; set; }

        /// <summary>Wer ihn geloest hat.</summary>
        public string? LastSolver { get; set; }

        /// <summary>Erst am Ende: ob dieser Spieler die meisten geloest hat.</summary>
        public bool IsWinner { get; set; }

        public List<string> WinnerNames { get; set; } = new();
    }

    /// <summary>
    /// Der Buchstabensalat am Server (M35 C5): die Begriffe bleiben hier, je Runde neu verwuerfelt; alle sehen
    /// denselben offenen Begriff. Den loest, wessen richtige Eingabe zuerst eintrifft - unter der Sperre des
    /// Rahmens, also an der Serveruhr; eine zweite richtige traegt dann die alte Nummer und wird verworfen. Danach
    /// kommt der naechste Begriff fuer alle (<see cref="MinigameSeenByAll"/>). Sind alle geloest, schliesst die Runde,
    /// sonst die Fristwache.
    /// </summary>
    public sealed class WordScrambleRules : IMinigameDeadlineRules
    {
        private readonly MinigameRulesContext kontext;
        private readonly TimeSpan vorlauf;
        private readonly TimeSpan dauer;
        private readonly IReadOnlyList<string> begriffe;
        private string[] wirrwarr = [];
        private int offen;
        private (string Begriff, Guid Loeser)? zuletzt;
        private TimeSpan start;
        private bool laeuft;
        private Dictionary<Guid, Rater> laufend = new();
        private Dictionary<Guid, BuchstabensalatStand> gezaehlt = new();

        public WordScrambleRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            this.kontext = kontext;
            vorlauf = TimeSpan.FromSeconds(Math.Max(0, setup.CountdownSeconds));
            dauer = TimeSpan.FromSeconds(Math.Max(1, setup.DurationSeconds));

            // Was sich nicht verwuerfeln laesst, stuende als Loesung am Telefon - es wird nicht gespielt.
            begriffe = (setup.Content ?? []).Select(z => z.Text).Where(BuchstabensalatBegriffe.Verwuerfelbar).ToList();
        }

        public MinigameKind Kind => MinigameKind.WordScramble;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => laufend.ToDictionary(r => r.Key, r => (object)r.Value.Stand);

        /// <summary>Seit wann gespielt wird; im Vorlauf negativ.</summary>
        private TimeSpan Laufzeit => kontext.Jetzt() - start;

        private bool AlleGeloest => begriffe.Count > 0 && offen >= begriffe.Count;

        public bool IsDue => laeuft && Laufzeit >= dauer;

        public void StartRound(int round)
        {
            wirrwarr = begriffe.Select(b => BuchstabensalatBegriffe.Verwuerfle(b, kontext.Zufall)!).ToArray();
            offen = 0;
            zuletzt = null;
            start = kontext.Jetzt() + vorlauf;
            laufend = new();
            gezaehlt = new();
            laeuft = true;
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (MinigamePayload.Read<WordScramblePayload>(payload) is not { Guess: { } eingabe } abgabe || string.IsNullOrWhiteSpace(eingabe))
                return null;

            var jetzt = kontext.Jetzt();
            var zeit = jetzt - start;

            // Im Vorlauf ist noch kein Begriff offen; ab der Frist zaehlt nichts mehr, auch vor dem Schliessen.
            if (!laeuft || zeit < TimeSpan.Zero || zeit >= dauer)
                return null;

            if (!laufend.TryGetValue(player.Id, out var rater))
                laufend[player.Id] = rater = new Rater();

            // Ein schon geloester Begriff (zu spaet, doppelt gesendet) zaehlt nicht noch einmal.
            var nummer = offen + 1;
            if (abgabe.TermNumber != nummer || AlleGeloest || !rater.Takt.TryTake(jetzt))
                return null;

            if (!BuchstabensalatBegriffe.Gleich(eingabe, begriffe[offen]))
            {
                rater.FalschBei = nummer;
                return new BuchstabensalatVersuch(nummer, null, rater.Stand);
            }

            rater.Geloest++;
            zuletzt = (begriffe[offen], player.Id);
            offen++;

            return new MinigameSeenByAll(new BuchstabensalatVersuch(nummer, zuletzt.Value.Begriff, rater.Stand));
        }

        /// <summary>Sind alle Begriffe geloest, hat jeder abgegeben - dann schliesst die Runde.</summary>
        public bool HasSubmitted(Guid playerId) => AlleGeloest;

        public void CloseRound()
        {
            gezaehlt = laufend.ToDictionary(r => r.Key, r => r.Value.Stand);
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
            zuletzt = null;
            laeuft = false;
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var info = new WordScrambleClientInfo
            {
                DurationSeconds = (int)dauer.TotalSeconds,
                TotalTerms = begriffe.Count,
                LastSolved = zuletzt?.Begriff,
                LastSolver = zuletzt is { } z ? kontext.NameVon(z.Loeser) ?? "?" : null,
            };

            if (phase == MinigamePhase.Running && laeuft)
            {
                if (!AlleGeloest)
                {
                    info.TermNumber = offen + 1;
                    info.Scramble = wirrwarr[offen];
                }

                info.RemainingMs = (int)Math.Clamp((dauer - Laufzeit).TotalMilliseconds, 0, dauer.TotalMilliseconds);
            }

            if (player == null)
                return info;

            var rater = laufend.GetValueOrDefault(player.Id);
            info.OwnSolved = (rater?.Stand ?? gezaehlt.GetValueOrDefault(player.Id))?.Geloest ?? 0;
            info.LastWrong = info.TermNumber > 0 && rater?.FalschBei == info.TermNumber;

            if (phase == MinigamePhase.Finished)
            {
                var gewinner = new BuchstabensalatErgebnis(gezaehlt, begriffe.Count, new Dictionary<Guid, Punktevorschlag>()).Gewinner;
                info.IsWinner = gewinner.Contains(player.Id);
                info.WinnerNames = gewinner.Select(id => kontext.NameVon(id) ?? "?").OrderBy(n => n).ToList();
            }

            return info;
        }

        /// <summary>Der Stand eines Telefons in der laufenden Runde.</summary>
        private sealed class Rater
        {
            public int Geloest { get; set; }

            /// <summary>Der Begriff, an dem der letzte Versuch danebenlag.</summary>
            public int FalschBei { get; set; }

            public SlidingRateLimit Takt { get; } = new(
                BuchstabensalatEvaluator.MaxVersucheJeFenster, TimeSpan.FromSeconds(BuchstabensalatEvaluator.FensterSekunden));

            public BuchstabensalatStand Stand => new(Geloest);
        }
    }
}
