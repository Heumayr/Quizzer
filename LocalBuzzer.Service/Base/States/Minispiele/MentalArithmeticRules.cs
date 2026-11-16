using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon beim Kopfrechnen abgibt. Feldnamen = <c>kopfrechnen.js</c>.</summary>
    public sealed class MentalArithmeticPayload
    {
        /// <summary>Die Nummer der Aufgabe, die das Telefon zeigte - eine andere als die laufende wird verworfen.</summary>
        public int TaskNumber { get; set; }

        /// <summary>Die Antwort; fehlt sie, ist die Abgabe kaputt.</summary>
        public int? Answer { get; set; }
    }

    /// <summary>Was ein Telefon beim Kopfrechnen sieht - seine laufende Aufgabe, nie eine Loesung.</summary>
    public sealed class MentalArithmeticClientInfo
    {
        public int DurationSeconds { get; set; }

        /// <summary>So viel Zeit bleibt an der Serveruhr, gemessen beim Versand; <c>null</c> ausserhalb der Runde.</summary>
        public int? RemainingMs { get; set; }

        /// <summary>Die laufende Aufgabe dieses Telefons, ab 1; 0 ausserhalb der Runde.</summary>
        public int TaskNumber { get; set; }

        /// <summary>„7 + 5" - nur waehrend der Runde und nur fuer einen Spieler.</summary>
        public string? TaskText { get; set; }

        public int OwnCorrect { get; set; }

        public int OwnAnswered { get; set; }

        /// <summary>Ob die letzte Antwort richtig war - fuer den Hinweis am Telefon; <c>null</c> vor der ersten.</summary>
        public bool? LastCorrect { get; set; }

        /// <summary>Erst am Ende: ob dieser Spieler gewonnen hat.</summary>
        public bool IsWinner { get; set; }

        public List<string> WinnerNames { get; set; } = new();

        public int? WinnerCorrect { get; set; }
    }

    /// <summary>
    /// Das Kopfrechnen am Server (M35 C5): der Startwert der Aufgabenfolge und damit jede Loesung bleibt hier.
    /// Jede Antwort wird an der Serveruhr geprueft und bringt die naechste Aufgabe fuer genau dieses Telefon.
    /// Die Runde schliesst die Fristwache nach <c>DurationSeconds</c>; davor laeuft der Countdown der Telefone.
    /// </summary>
    public sealed class MentalArithmeticRules : IMinigameDeadlineRules
    {
        private readonly MinigameRulesContext kontext;
        private readonly TimeSpan vorlauf;
        private readonly TimeSpan dauer;
        private readonly int stufe;
        private int startwert;
        private TimeSpan start;
        private bool laeuft;
        private Dictionary<Guid, Rechner> laufend = new();
        private Dictionary<Guid, KopfrechnenStand> gezaehlt = new();

        public MentalArithmeticRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            this.kontext = kontext;
            vorlauf = TimeSpan.FromSeconds(Math.Max(0, setup.CountdownSeconds));
            dauer = TimeSpan.FromSeconds(Math.Max(1, setup.DurationSeconds));
            stufe = setup.Variant;
        }

        public MinigameKind Kind => MinigameKind.MentalArithmetic;

        /// <summary>Die Staende der gezaehlten Runde.</summary>
        public IReadOnlyDictionary<Guid, KopfrechnenStand> Endstaende => gezaehlt;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => laufend.ToDictionary(r => r.Key, r => (object)r.Value.Stand);

        /// <summary>Seit wann gerechnet wird; im Vorlauf negativ.</summary>
        private TimeSpan Laufzeit => kontext.Jetzt() - start;

        public bool IsDue => laeuft && Laufzeit >= dauer;

        public void StartRound(int round)
        {
            startwert = kontext.Zufall(0, int.MaxValue);
            start = kontext.Jetzt() + vorlauf;
            laufend = new();
            gezaehlt = new();
            laeuft = true;
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (MinigamePayload.Read<MentalArithmeticPayload>(payload) is not { Answer: { } antwort } abgabe)
                return null;

            var jetzt = kontext.Jetzt();
            var zeit = jetzt - start;

            // Im Vorlauf steht noch keine Aufgabe; ab der Frist zaehlt nichts mehr, auch vor dem Schliessen.
            if (!laeuft || zeit < TimeSpan.Zero || zeit >= dauer)
                return null;

            if (!laufend.TryGetValue(player.Id, out var rechner))
                laufend[player.Id] = rechner = new Rechner();

            // Eine alte Aufgabe (doppelt gesendet, nach dem Wiederverbinden) zaehlt nicht noch einmal.
            if (abgabe.TaskNumber != rechner.Aufgabe || !rechner.Takt.TryTake(jetzt))
                return null;

            var richtig = antwort == KopfrechnenAufgaben.Aufgabe(startwert, rechner.Aufgabe, stufe).Loesung;
            var nummer = rechner.Aufgabe;

            rechner.Beantworte(richtig);
            return new KopfrechnenAntwort(nummer, richtig, rechner.Stand);
        }

        /// <summary>Abgegeben hat beim Kopfrechnen keiner - jede Antwort bringt die naechste Aufgabe, bis die Zeit um ist.</summary>
        public bool HasSubmitted(Guid playerId) => false;

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
            laeuft = false;
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var info = new MentalArithmeticClientInfo { DurationSeconds = (int)dauer.TotalSeconds };

            if (player == null)
                return info;

            var rechner = laufend.GetValueOrDefault(player.Id);
            var stand = rechner?.Stand ?? gezaehlt.GetValueOrDefault(player.Id);

            info.OwnCorrect = stand?.Richtig ?? 0;
            info.OwnAnswered = stand?.Beantwortet ?? 0;
            info.LastCorrect = rechner?.LetzteRichtig;

            if (phase == MinigamePhase.Running && laeuft)
            {
                info.TaskNumber = rechner?.Aufgabe ?? 1;
                info.TaskText = KopfrechnenAufgaben.Aufgabe(startwert, info.TaskNumber, stufe).Text;
                info.RemainingMs = (int)Math.Clamp((dauer - Laufzeit).TotalMilliseconds, 0, dauer.TotalMilliseconds);
            }

            if (phase == MinigamePhase.Finished)
            {
                var gewinner = KopfrechnenEvaluator.Gewinner(gezaehlt);
                info.IsWinner = gewinner.Contains(player.Id);
                info.WinnerNames = gewinner.Select(id => kontext.NameVon(id) ?? "?").OrderBy(n => n).ToList();
                info.WinnerCorrect = gewinner.Count > 0 ? gezaehlt[gewinner.First()].Richtig : null;
            }

            return info;
        }

        /// <summary>Der Stand eines Telefons in der laufenden Runde.</summary>
        private sealed class Rechner
        {
            public int Aufgabe { get; private set; } = 1;

            public int Richtig { get; private set; }

            public bool? LetzteRichtig { get; private set; }

            public SlidingRateLimit Takt { get; } = new(KopfrechnenEvaluator.MaxAntwortenJeSekunde, TimeSpan.FromSeconds(1));

            public KopfrechnenStand Stand => new(Richtig, Aufgabe - 1);

            public void Beantworte(bool richtig)
            {
                if (richtig)
                    Richtig++;

                LetzteRichtig = richtig;
                Aufgabe++;
            }
        }
    }
}
