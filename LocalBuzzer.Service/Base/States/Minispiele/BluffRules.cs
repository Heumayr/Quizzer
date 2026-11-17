using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon beim Bluff abgibt: in Phase 1 einen Bluff, in Phase 2 eine Wahl. Feldnamen = <c>bluff.js</c>.</summary>
    public sealed class BluffPayload
    {
        public string? Bluff { get; set; }

        /// <summary>Die Nummer der gewaehlten Antwort, wie das Telefon sie bekam.</summary>
        public int? Choice { get; set; }
    }

    /// <summary>Eine Antwort am Telefon - ohne Urheber und ohne „echt".</summary>
    public sealed class BluffOptionInfo
    {
        public int Number { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Was ein Telefon beim Bluff sieht. <b>Nie, wer welchen Bluff schrieb, und vor dem Ende nicht, welche Antwort echt
    /// ist</b> - die Antworten kommen gemischt, ohne Kennzeichen; der eigene Bluff fehlt in der Liste.
    /// </summary>
    public sealed class BluffClientInfo
    {
        public int DurationSeconds { get; set; }

        /// <summary>So viel Zeit bleibt an der Serveruhr, gemessen beim Versand; <c>null</c> ausserhalb der Runde.</summary>
        public int? RemainingMs { get; set; }

        /// <summary>1 = Bluffs schreiben, 2 = waehlen; 0 vor der ersten Phase.</summary>
        public int Stage { get; set; }

        /// <summary>Der eigene Bluff - nur fuer dieses Telefon.</summary>
        public string? OwnBluff { get; set; }

        /// <summary>Ab Phase 2: die Antworten zur Wahl, ohne den eigenen Bluff.</summary>
        public List<BluffOptionInfo> Options { get; set; } = new();

        public int? OwnChoice { get; set; }

        /// <summary>Erst nach der Wahl: die echte Antwort.</summary>
        public string? RealAnswer { get; set; }

        /// <summary>Erst nach der Wahl: ob dieses Telefon die echte gewaehlt hat.</summary>
        public bool FoundReal { get; set; }

        /// <summary>Erst nach der Wahl: so viele sind auf den eigenen Bluff hereingefallen.</summary>
        public int FooledCount { get; set; }

        /// <summary>Erst nach der Wahl: der eigene Bluff war gestrichen - vorher verriete es die echte Antwort.</summary>
        public bool OwnStruck { get; set; }
    }

    /// <summary>Ein Bluff fuer den Spielleiter zwischen den Phasen - gleiche zusammengefasst, ohne Urheber.</summary>
    /// <param name="Schluessel">Die Vergleichsform - damit streicht der Spielleiter.</param>
    /// <param name="VonSelbst">Gleicht der echten Antwort (<see cref="BuchstabensalatBegriffe.Gleich"/>) - bleibt gestrichen.</param>
    public sealed record BluffEintrag(string Schluessel, string Text, int Anzahl, bool Gestrichen, bool VonSelbst);

    /// <summary>
    /// Der Bluff am Server (M35 C6), zwei Phasen als zwei Runden des Rahmens. Phase 1: jeder schreibt eine falsche Antwort
    /// - eine Wahl nimmt der Server hier nicht an. Danach streicht der Spielleiter, was der echten gleicht
    /// (<see cref="Streiche"/>); was ihr schon nach <see cref="BuchstabensalatBegriffe.Gleich"/> gleicht, ist von selbst
    /// gestrichen. Phase 2: jeder waehlt unter den uebrigen Bluffs und der echten Antwort, gemischt; gleiche Bluffs stehen
    /// einmal da, der eigene fehlt - und wird auch am Server abgewiesen. Einen Bluff nimmt der Server hier nicht an.
    /// Die echte Antwort ist die erste Zeile; weitere Zeilen gelten als gleichwertige Schreibweisen.
    /// </summary>
    public sealed class BluffRules : IMinigameDeadlineRules
    {
        private readonly MinigameRulesContext kontext;
        private readonly TimeSpan vorlauf;
        private readonly TimeSpan dauer;
        private readonly string echt;
        private readonly IReadOnlyList<string> schreibweisen;
        private int phase;
        private bool laeuft;
        private bool aufgeloest;
        private TimeSpan start;
        private Dictionary<Guid, string> bluffs = new();
        private HashSet<string> gestrichen = new();
        private List<BluffOption> optionen = new();
        private Dictionary<Guid, int> wahl = new();

        public BluffRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            this.kontext = kontext;
            vorlauf = TimeSpan.FromSeconds(Math.Max(0, setup.CountdownSeconds));
            dauer = TimeSpan.FromSeconds(Math.Max(1, setup.DurationSeconds));
            schreibweisen = (setup.Content ?? []).Select(z => z.Text).Where(t => t.Any(char.IsLetterOrDigit)).ToList();
            echt = schreibweisen.FirstOrDefault() ?? string.Empty;
        }

        public MinigameKind Kind => MinigameKind.Bluff;

        /// <summary>Fuer den Spielleiter: die echte Antwort - nie fuer ein Telefon vor dem Ende.</summary>
        public string EchteAntwort => echt;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => (phase == 1 ? bluffs.Keys.ToList() : wahl.Keys.ToList()).ToDictionary(id => id, _ => (object)new BluffAbgabe(phase));

        private TimeSpan Laufzeit => kontext.Jetzt() - start;

        public bool IsDue => laeuft && Laufzeit >= dauer;

        /// <summary>Zwischen den Phasen fuer den Spielleiter: die Bluffs, gleiche zusammengefasst; sonst leer.</summary>
        public IReadOnlyList<BluffEintrag> Eingaenge
            => phase == 1 && !laeuft
                ? Gruppen().Select(g => new BluffEintrag(g.Schluessel, g.Text, g.Urheber.Count, g.VonSelbst || gestrichen.Contains(g.Schluessel), g.VonSelbst)).ToList()
                : [];

        /// <summary>Erst nach der Wahl: alle Antworten samt Urheber und die Wahl je Spieler - fuer die Wertung.</summary>
        public BluffRunde? Auswertung => aufgeloest ? new(optionen.ToList(), new Dictionary<Guid, int>(wahl)) : null;

        public void StartRound(int round)
        {
            phase = round >= 2 ? 2 : 1;

            if (phase == 1)
            {
                bluffs = new();
                gestrichen = new();
                optionen = new();
            }
            else
            {
                optionen = BaueOptionen();
            }

            wahl = new();
            aufgeloest = false;
            start = kontext.Jetzt() + vorlauf;
            laeuft = true;
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (MinigamePayload.Read<BluffPayload>(payload) is not { } abgabe)
                return null;

            var zeit = kontext.Jetzt() - start;
            if (!laeuft || zeit < TimeSpan.Zero || zeit >= dauer)
                return null;

            // Phase 1 nimmt nur Bluffs, Phase 2 nur Wahlen - eine Abgabe der anderen Phase ist alt oder gefaelscht.
            if (phase == 1)
            {
                if (abgabe.Choice != null || string.IsNullOrWhiteSpace(abgabe.Bluff) || bluffs.ContainsKey(player.Id))
                    return null;

                var text = abgabe.Bluff.Trim();
                bluffs[player.Id] = text.Length > BluffEvaluator.MaxZeichen ? text[..BluffEvaluator.MaxZeichen] : text;
                return new BluffAbgabe(1);
            }

            if (abgabe.Bluff != null || abgabe.Choice is not { } nummer || wahl.ContainsKey(player.Id))
                return null;

            // Den eigenen Bluff waehlen hiesse, sich selbst Punkte zu schenken - das Telefon zeigt ihn nicht, der Server nimmt ihn nicht.
            if (optionen.FirstOrDefault(o => o.Nummer == nummer && o.Nummer > 0) is not { Gestrichen: false } option
                || option.Urheber.Contains(player.Id))
                return null;

            wahl[player.Id] = nummer;
            return new BluffAbgabe(2);
        }

        public bool HasSubmitted(Guid playerId) => phase == 1 ? bluffs.ContainsKey(playerId) : phase == 2 && wahl.ContainsKey(playerId);

        public void CloseRound()
        {
            laeuft = false;
            aufgeloest = phase == 2;
        }

        public void DiscardRunning()
        {
            laeuft = false;
            aufgeloest = false;
        }

        public void Clear()
        {
            phase = 0;
            laeuft = false;
            aufgeloest = false;
            bluffs = new();
            gestrichen = new();
            optionen = new();
            wahl = new();
        }

        /// <summary>Der Spielleiter streicht einen Bluff zwischen den Phasen (oder nimmt das zurueck); <c>false</c> sonst.</summary>
        public bool Streiche(string schluessel, bool streichen)
        {
            if (phase != 1 || laeuft || Gruppen().FirstOrDefault(g => g.Schluessel == schluessel) is not { VonSelbst: false })
                return false;

            if (streichen)
                gestrichen.Add(schluessel);
            else
                gestrichen.Remove(schluessel);

            return true;
        }

        public object? InfoFor(Player? player, MinigamePhase rahmen)
        {
            var info = new BluffClientInfo { DurationSeconds = (int)dauer.TotalSeconds, Stage = phase };

            if (rahmen == MinigamePhase.Running && laeuft)
                info.RemainingMs = (int)Math.Clamp((dauer - Laufzeit).TotalMilliseconds, 0, dauer.TotalMilliseconds);

            if (phase == 2)
                info.Options = optionen
                    .Where(o => o.Nummer > 0 && (player == null || !o.Urheber.Contains(player.Id)))
                    .Select(o => new BluffOptionInfo { Number = o.Nummer, Text = o.Text })
                    .ToList();

            if (aufgeloest)
                info.RealAnswer = echt;

            if (player == null)
                return info;

            info.OwnBluff = bluffs.GetValueOrDefault(player.Id);
            info.OwnChoice = wahl.TryGetValue(player.Id, out var gewaehlt) ? gewaehlt : null;

            if (aufgeloest && Auswertung is { } runde)
            {
                var wertung = new BluffErgebnis(runde, 0, 0, new Dictionary<Guid, Punktevorschlag>()) { Teamkollegen = kontext.Teamkollegen };
                info.FoundReal = wertung.Gefunden(player.Id);
                info.FooledCount = wertung.Reinfaelle(player.Id);
                info.OwnStruck = optionen.Any(o => o.Gestrichen && o.Urheber.Contains(player.Id));
            }

            return info;
        }

        private sealed record Gruppe(string Schluessel, string Text, List<Guid> Urheber, bool VonSelbst);

        /// <summary>Gleiche Bluffs (Vergleichsform) zusammengefasst; der Text ist unabhaengig davon, wer zuerst schrieb.</summary>
        private List<Gruppe> Gruppen()
            => bluffs
                .GroupBy(b => BuchstabensalatBegriffe.Normalisiere(b.Value))
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new Gruppe(
                    g.Key,
                    g.Select(b => b.Value).OrderBy(t => t, StringComparer.Ordinal).First(),
                    g.Select(b => b.Key).ToList(),
                    schreibweisen.Any(s => BuchstabensalatBegriffe.Gleich(g.Key, s))))
                .ToList();

        /// <summary>
        /// Die Antworten der Phase 2: gestrichene mit Nummer 0, die uebrigen samt der echten erst nach Text geordnet, dann
        /// gemischt - so haengt die Reihenfolge nur vom Zufall ab, nie davon, welche echt ist oder wer sie schrieb.
        /// </summary>
        private List<BluffOption> BaueOptionen()
        {
            var gruppen = Gruppen();
            var wahlbar = gruppen
                .Where(g => !g.VonSelbst && !gestrichen.Contains(g.Schluessel))
                .Select(g => (g.Text, Echt: false, Urheber: (IReadOnlyList<Guid>)g.Urheber))
                .Append((Text: echt, Echt: true, Urheber: (IReadOnlyList<Guid>)[]))
                .OrderBy(o => BuchstabensalatBegriffe.Normalisiere(o.Text), StringComparer.Ordinal)
                .ToArray();

            for (var i = wahlbar.Length - 1; i > 0; i--)
            {
                var j = Math.Clamp(kontext.Zufall(0, i + 1), 0, i);
                (wahlbar[i], wahlbar[j]) = (wahlbar[j], wahlbar[i]);
            }

            return wahlbar
                .Select((o, i) => new BluffOption(i + 1, o.Text, o.Echt, o.Urheber))
                .Concat(gruppen
                    .Where(g => g.VonSelbst || gestrichen.Contains(g.Schluessel))
                    .Select(g => new BluffOption(0, g.Text, false, g.Urheber, Gestrichen: true)))
                .ToList();
        }
    }
}
