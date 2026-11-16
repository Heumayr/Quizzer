using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon im Duell bei Schere-Stein-Papier abgibt. Feldname = <c>duell.js</c>.</summary>
    public sealed class DuelPayload
    {
        public SchereSteinPapier? Move { get; set; }
    }

    /// <summary>
    /// Was ein Telefon im Duell sieht. <b>Den Zug des Gegners erst, wenn die Runde zu ist</b> - vorher
    /// erfuehre ein Telefon die fremde Wahl.
    /// </summary>
    public sealed class DuelClientInfo
    {
        /// <summary>Die Disziplin: <see cref="DuellEvaluator.SchereSteinPapierVariant"/> oder <see cref="DuellEvaluator.ReaktionVariant"/>.</summary>
        public int Discipline { get; set; }

        /// <summary>Dieses Telefon waehlt gerade den Gegner.</summary>
        public bool ChoosesOpponent { get; set; }

        /// <summary>Die Tasten mit den Spielernamen - nur fuer den Waehlenden.</summary>
        public List<MinigamePlayerOption> Options { get; set; } = new();

        public string? ChallengerName { get; set; }

        public string? OpponentName { get; set; }

        /// <summary>Ob dieses Telefon im Duell spielt - sonst schaut es zu.</summary>
        public bool IsDuellist { get; set; }

        /// <summary>Der eigene Zug der laufenden bzw. zuletzt geschlossenen Runde.</summary>
        public SchereSteinPapier? OwnMove { get; set; }

        /// <summary>Der Zug des Gegners - erst nach dem Schliessen der Runde.</summary>
        public SchereSteinPapier? OtherMove { get; set; }

        public int OwnWins { get; set; }

        public int OtherWins { get; set; }

        /// <summary>Bei der Disziplin Reaktion: was <c>reaktion.js</c> braucht.</summary>
        public ReactionClientInfo? Reaction { get; set; }

        /// <summary>Erst am Ende.</summary>
        public bool IsWinner { get; set; }

        public bool IsDraw { get; set; }

        public string? WinnerName { get; set; }
    }

    /// <summary>
    /// Das Duell am Server (M35 C2): erst waehlt der Ausloeser den Gegner (<see cref="PartnerChoice"/>),
    /// dann spielen nur die beiden - Schere-Stein-Papier (Best of n) oder Reaktion (<see cref="ReactionRules"/>).
    /// </summary>
    public sealed class DuelRules : IMinigamePairRules
    {
        private readonly PartnerChoice wahl;
        private readonly ReactionRules reaktion;
        private readonly int disziplin;
        private readonly int runden;
        private readonly SortedDictionary<int, Dictionary<Guid, SchereSteinPapier>> geschlossen = new();
        private Dictionary<Guid, SchereSteinPapier> zuege = new();
        private int runde;

        public DuelRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            ArgumentNullException.ThrowIfNull(kontext);

            wahl = new PartnerChoice(setup.TriggerPlayerId, "Gegner", kontext);
            reaktion = new ReactionRules(kontext.Zufall, kontext.NameVon);
            disziplin = setup.Variant == DuellEvaluator.ReaktionVariant ? DuellEvaluator.ReaktionVariant : DuellEvaluator.SchereSteinPapierVariant;
            runden = Math.Max(1, setup.TotalRounds);
        }

        public MinigameKind Kind => MinigameKind.Duel;

        private bool IstReaktion => disziplin == DuellEvaluator.ReaktionVariant;

        public Guid? ChooserId => wahl.Chooser;

        public Guid? PartnerId => wahl.Partner;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => IstReaktion ? reaktion.CurrentSubmissions : zuege.ToDictionary(z => z.Key, z => (object)z.Value);

        public bool Plays(Guid playerId) => wahl.Plays(playerId);

        public string? WatcherText => wahl.WatcherText;

        public bool IsDecided => !IstReaktion && Ergebnis() is { } e && DuellEvaluator.IstEntschieden(e, runden);

        /// <summary>Der Stand ueber die gezaehlten Runden; <c>null</c>, solange kein Gegner gewaehlt ist.</summary>
        public DuellErgebnis? Ergebnis()
        {
            if (wahl is not { Chooser: { } a, Partner: { } b })
                return null;

            return IstReaktion
                ? DuellEvaluator.WerteReaktion(a, b, reaktion.ClosedRounds)
                : DuellEvaluator.WerteSchereSteinPapier(a, b, geschlossen.Values);
        }

        public void StartRound(int round)
        {
            runde = round;
            zuege = new();
            geschlossen.Remove(round);
            reaktion.StartRound(round);
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (!wahl.Chosen)
                return wahl.Accept(player, payload);

            if (IstReaktion)
                return reaktion.Accept(player, payload);

            if (MinigamePayload.Read<DuelPayload>(payload)?.Move is not { } zug || !Enum.IsDefined(zug))
                return null;

            zuege[player.Id] = zug;
            return zug;
        }

        public bool HasSubmitted(Guid playerId)
            => wahl.Chosen && (IstReaktion ? reaktion.HasSubmitted(playerId) : zuege.ContainsKey(playerId));

        public void CloseRound()
        {
            if (IstReaktion)
                reaktion.CloseRound();
            else
                geschlossen[runde] = zuege;
        }

        public void DiscardRunning()
        {
            zuege = new();
            reaktion.DiscardRunning();
        }

        public void Clear()
        {
            geschlossen.Clear();
            zuege = new();
            runde = 0;
            reaktion.Clear();
            wahl.Clear();
        }

        /// <summary>Der Spielleiter legt fest, wer spielt (E1); was in der laufenden Runde schon abgegeben war, zaehlt nicht.</summary>
        public bool SetPair(Guid chooserId, Guid? partnerId)
        {
            if (!wahl.SetPair(chooserId, partnerId))
                return false;

            DiscardRunning();
            return true;
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var info = new DuelClientInfo
            {
                Discipline = disziplin,
                ChallengerName = wahl.ChooserName,
                OpponentName = wahl.PartnerName,
                ChoosesOpponent = player != null && phase == MinigamePhase.Running && wahl.ChoosesNow(player.Id),
                Options = phase == MinigamePhase.Running ? wahl.OptionsFor(player) : new(),
            };

            var ergebnis = Ergebnis();

            if (ergebnis == null)
                return info;

            if (phase == MinigamePhase.Finished)
            {
                info.IsDraw = ergebnis.Sieger == null;
                info.WinnerName = ergebnis.Sieger is { } sieger ? wahl.Chooser == sieger ? wahl.ChooserName : wahl.PartnerName : null;
            }

            if (player == null || !ergebnis.Spielt(player.Id))
                return info;

            FuelleDuellant(info, player, phase, ergebnis);
            return info;
        }

        private void FuelleDuellant(DuelClientInfo info, Player player, MinigamePhase phase, DuellErgebnis ergebnis)
        {
            var anderer = player.Id == ergebnis.Herausforderer ? ergebnis.Gegner : ergebnis.Herausforderer;

            info.IsDuellist = true;
            info.OwnWins = ergebnis.SiegeVon(player.Id);
            info.OtherWins = ergebnis.SiegeVon(anderer);
            info.IsWinner = phase == MinigamePhase.Finished && ergebnis.Sieger == player.Id;

            if (IstReaktion)
            {
                info.Reaction = (ReactionClientInfo?)reaktion.InfoFor(player, phase);
                return;
            }

            var gezeigt = phase == MinigamePhase.Running ? zuege : geschlossen.GetValueOrDefault(runde);

            info.OwnMove = gezeigt != null && gezeigt.TryGetValue(player.Id, out var eigener) ? eigener : null;

            // Geheim bis zur Aufloesung: solange die Runde laeuft, nie der fremde Zug.
            info.OtherMove = phase != MinigamePhase.Running && gezeigt != null && gezeigt.TryGetValue(anderer, out var fremder)
                ? fremder
                : null;
        }
    }
}
