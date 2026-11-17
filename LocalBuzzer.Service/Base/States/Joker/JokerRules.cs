using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Anzeige;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Joker
{
    /// <summary>
    /// Die Joker-Runde am Server (M35 C7b), im Rahmen der Minispiele (<see cref="MinigameState"/>, eine Runde): wer
    /// offene Joker haelt, waehlt einen eigenen oder keinen, bei der Sperre dazu ein Ziel unter den uebrigen
    /// Mitspielern. <b>Jede Abgabe kommt aus dem WLAN</b>: fremde, unbekannte oder schon gesetzte Joker und falsche
    /// Ziele werden verworfen, und je Spieler gilt eine Plausibilitaetsgrenze.
    /// <para>Geheim bis zum Schluss: vor dem Schliessen sieht jedes Telefon nur sich selbst, der Beamer nichts.</para>
    /// </summary>
    public sealed class JokerRules : IMinigameRules
    {
        /// <summary>So viele Versuche je Spieler im Fenster - ein ehrliches Telefon gibt genau einmal ab.</summary>
        public const int MaxVersucheJeFenster = 5;

        public static readonly TimeSpan Fenster = TimeSpan.FromSeconds(10);

        public const string ZuschauerText = "Joker-Runde läuft – du hast keinen Joker. Gleich kommt die Frage.";

        private readonly MinigameRulesContext kontext;
        private readonly IReadOnlyDictionary<Guid, IReadOnlyList<JokerOffer>> inventar;
        private readonly Dictionary<Guid, SlidingRateLimit> takt = new();
        private Dictionary<Guid, JokerChoice> laufend = new();
        private bool zu;
        private bool bestaetigt;
        private HashSet<Guid> abgelehnt = new();

        public JokerRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            ArgumentNullException.ThrowIfNull(kontext);

            this.kontext = kontext;
            inventar = (setup.Jokers ?? new Dictionary<Guid, IReadOnlyList<JokerOffer>>())
                .Where(e => e.Value.Count > 0)
                .ToDictionary(e => e.Key, e => (IReadOnlyList<JokerOffer>)e.Value.ToList());
        }

        /// <summary>Die Joker-Runde ist keine Minispiel-Art - die Art bleibt leer.</summary>
        public MinigameKind Kind => default;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => laufend.ToDictionary(e => e.Key, e => (object)e.Value);

        public string? WatcherText => ZuschauerText;

        /// <summary>Nur wer einen offenen Joker haelt, entscheidet - die anderen schauen zu.</summary>
        public bool Plays(Guid playerId) => inventar.ContainsKey(playerId);

        public void StartRound(int round) => Oeffne();

        public object? Accept(Player player, JsonElement payload)
        {
            if (!takt.TryGetValue(player.Id, out var grenze))
                takt[player.Id] = grenze = new(MaxVersucheJeFenster, Fenster);

            if (!grenze.TryTake(kontext.Jetzt()) || Pruefe(player, MinigamePayload.Read<JokerPayload>(payload)) is not { } wahl)
                return null;

            laufend[player.Id] = wahl;
            return wahl;
        }

        public bool HasSubmitted(Guid playerId) => laufend.ContainsKey(playerId);

        public void CloseRound() => zu = true;

        /// <summary>Abgebrochen: keine Wahl zaehlt, also gibt es nichts zu bestaetigen.</summary>
        public void DiscardRunning()
        {
            Oeffne();
            zu = bestaetigt = true;
        }

        public void Clear() => Oeffne();

        private void Oeffne()
        {
            laufend = new();
            zu = bestaetigt = false;
            abgelehnt = new();
        }

        /// <summary>
        /// Der Spielleiter hat eingeloest (V12, zweiter Riegel im WPF): erst jetzt sehen die Telefone, wer welchen Joker
        /// setzt, und nur, was er angenommen hat; wer abgelehnt wurde, erfaehrt es. <c>false</c>, solange die Runde laeuft.
        /// </summary>
        public bool ConfirmRedeemed(IReadOnlyCollection<Guid> eingeloest)
        {
            ArgumentNullException.ThrowIfNull(eingeloest);

            if (!zu)
                return false;

            abgelehnt = laufend.Values.Where(w => w.JokerId is { } id && !eingeloest.Contains(id)).Select(w => w.PlayerId).ToHashSet();
            bestaetigt = true;
            return true;
        }

        /// <summary>Ergebnisse und Sperre erst, wenn die Runde zu ist und der Spielleiter eingeloest hat (V12).</summary>
        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var fertig = (phase is MinigamePhase.RoundOver or MinigamePhase.Finished) && bestaetigt;
            var eigene = player != null ? inventar.GetValueOrDefault(player.Id) ?? [] : [];
            var wahl = player != null ? laufend.GetValueOrDefault(player.Id) : null;

            return new JokerClientInfo
            {
                Options = eigene.Select(Taste).ToList(),
                Targets = player != null && eigene.Any(j => j.Kind == JokerKind.Block) ? Ziele(player.Id) : new(),
                OwnJokerId = wahl?.JokerId,
                OwnTarget = wahl?.TargetId,
                Confirmed = fertig,
                OwnRejected = fertig && player != null && abgelehnt.Contains(player.Id),
                Results = fertig ? Ergebnisse() : new(),
                BlockedBy = fertig && player != null ? Sperrer(player.Id) : null,
            };
        }

        /// <summary>Prueft eine Abgabe; <c>null</c> heisst verworfen.</summary>
        private JokerChoice? Pruefe(Player player, JokerPayload? abgabe)
        {
            if (abgabe == null)
                return null;

            if (abgabe.JokerId is not { } id)
                return abgabe.Target == null ? new JokerChoice(player.Id, null, null, null) : null;

            // Nur ein eigener, offener Joker - die Liste kennt nur diese.
            if (inventar.GetValueOrDefault(player.Id)?.FirstOrDefault(j => j.Id == id) is not { } joker)
                return null;

            if (joker.Kind != JokerKind.Block)
                return abgabe.Target == null ? new JokerChoice(player.Id, id, joker.Kind, null) : null;

            return abgabe.Target is { } ziel && IstZiel(player.Id, ziel)
                ? new JokerChoice(player.Id, id, joker.Kind, ziel)
                : null;
        }

        /// <summary>Gesperrt wird nur ein Mitspieler, nie man selbst - und im Teamspiel nur ein Gegner (M37 T4).</summary>
        private bool IstZiel(Guid ich, Guid ziel)
            => ziel != ich && kontext.Mitspieler().Any(p => p.Id == ziel) && !kontext.Teamkollegen(ich, ziel);

        private static JokerOptionInfo Taste(JokerOffer joker) => new()
        {
            Id = joker.Id,
            Name = JokerTexte.Name(joker.Kind),
            Effect = JokerTexte.Wirkung(joker.Kind),
            NeedsTarget = joker.Kind == JokerKind.Block,
        };

        private List<MinigamePlayerOption> Ziele(Guid ich)
            => kontext.Mitspieler()
                .Where(p => p.Id != ich && !kontext.Teamkollegen(ich, p.Id))
                .Select(p => new MinigamePlayerOption { Id = p.Id, Name = Name(p.Id) })
                .OrderBy(o => o.Name, StringComparer.CurrentCulture)
                .ToList();

        /// <summary>Die Wahlen, die gelten - eine vom Spielleiter abgelehnte zaehlt nirgends (V12).</summary>
        private IEnumerable<JokerChoice> Geltende => laufend.Values.Where(w => !abgelehnt.Contains(w.PlayerId));

        private List<JokerResultInfo> Ergebnisse()
            => Geltende
                .Select(w => w.Kind is { } art
                    ? new JokerResultInfo { Player = Name(w.PlayerId), Joker = JokerTexte.Name(art), Target = w.TargetId is { } ziel ? Name(ziel) : null }
                    : null)
                .OfType<JokerResultInfo>()
                .OrderBy(r => r.Player, StringComparer.CurrentCulture)
                .ToList();

        private string? Sperrer(Guid gesperrt)
        {
            var wer = Geltende.Where(w => w.Kind == JokerKind.Block && w.TargetId == gesperrt).Select(w => Name(w.PlayerId)).ToList();
            return wer.Count > 0 ? string.Join(", ", wer) : null;
        }

        private string Name(Guid id) => kontext.NameVon(id) ?? "?";
    }
}
