using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was ein Telefon beim Gluecksrad sieht - das Feld erst, wenn es fuer dieses Telefon zaehlt.</summary>
    public sealed class WheelClientInfo
    {
        /// <summary>Das Feld - fuer den, der gerade sein Ziel waehlt, und nach der Runde fuer alle.</summary>
        public string? SegmentName { get; set; }

        /// <summary>Dieses Telefon waehlt gerade das Ziel (Klauen, Verschenken).</summary>
        public bool ChoosesTarget { get; set; }

        public List<MinigamePlayerOption> Options { get; set; } = new();

        /// <summary>„Wen beklaust du?" bzw. „Wem schenkst du Punkte?".</summary>
        public string? ChoicePrompt { get; set; }

        /// <summary>Erst nach der Runde: was das Rad ergeben hat, ohne Punkte.</summary>
        public string? ResultText { get; set; }
    }

    /// <summary>Der Dreher hat sein Ziel gewaehlt - die Runde ist damit entschieden.</summary>
    public sealed record WheelTargetChosen(Guid Dreher, Guid Ziel);

    /// <summary>
    /// Das Gluecksrad am Server (M35 C4): der Spielleiter dreht (Rundenstart), das Feld wird hier gewuerfelt.
    /// Das Rad „landet" nach dem Countdown der Telefone; bei Klauen und Verschenken waehlt dann der Dreher
    /// am Telefon (<see cref="PartnerChoice"/>), sonst schliesst die Fristwache die Runde. Der Spielleiter
    /// kann Dreher und Ziel festlegen (E1). Gewertet wird im Fragefenster (<see cref="GluecksradEvaluator"/>).
    /// </summary>
    public sealed class WheelOfFortuneRules : IMinigamePairRules, IMinigameDeadlineRules
    {
        private readonly MinigameRulesContext kontext;
        private readonly PartnerChoice wahl;
        private readonly TimeSpan vorlauf;
        private WheelSegment? segment;
        private TimeSpan start;
        private bool laeuft;
        private bool gezaehlt;
        private bool telefonGewaehlt;

        /// <summary>Das Ziel kommt vom Spielleiter - dann waehlt das Telefon nicht mehr.</summary>
        private bool vorgegeben;

        public WheelOfFortuneRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            this.kontext = kontext;
            wahl = new PartnerChoice(setup.TriggerPlayerId, "Ziel", kontext);
            vorlauf = TimeSpan.FromSeconds(Math.Max(0, setup.CountdownSeconds));
        }

        public MinigameKind Kind => MinigameKind.WheelOfFortune;

        public Guid? ChooserId => wahl.Chooser;

        public Guid? PartnerId => wahl.Partner;

        /// <summary>Das gedrehte Feld - fuer den Spielleiter; <c>null</c> vor dem ersten Drehen.</summary>
        public WheelSegment? Segment => segment;

        /// <summary>Ob das Rad steht - an der Serveruhr, nach dem Countdown der Telefone.</summary>
        public bool Gelandet => segment != null && (!laeuft || kontext.Jetzt() - start >= vorlauf);

        private bool BrauchtZiel => segment is { } s && GluecksradEvaluator.BrauchtZiel(s);

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => telefonGewaehlt && wahl is { Chooser: { } dreher, Partner: { } ziel }
                ? new Dictionary<Guid, object> { [dreher] = new WheelTargetChosen(dreher, ziel) }
                : new Dictionary<Guid, object>();

        /// <summary>Nur der Dreher, nur bei Klauen und Verschenken, und nur, solange der Spielleiter das Ziel nicht vorgab.</summary>
        public bool Plays(Guid playerId) => laeuft && BrauchtZiel && !vorgegeben && playerId == wahl.Chooser;

        /// <summary>Gelandet und - wo noetig - ein Ziel da: dann schliesst die Fristwache.</summary>
        public bool IsDue => laeuft && Gelandet && (!BrauchtZiel || wahl.Chosen);

        public string? WatcherText => "Das Glücksrad dreht sich – schau auf den Beamer!";

        public void StartRound(int round)
        {
            // Im Teamspiel ohne Robin Hood (M37 T8) - die Aufstellung des Hubs sagt, ob es eines ist.
            var teamspiel = kontext.IstTeamspiel;
            segment = GluecksradEvaluator.Segment(kontext.Zufall(0, GluecksradEvaluator.FelderFuer(teamspiel).Count), teamspiel);
            start = kontext.Jetzt();
            laeuft = true;
            gezaehlt = false;
            telefonGewaehlt = false;

            // Neu gedreht heisst neu gewaehlt - ausser der Spielleiter hat das Ziel festgelegt.
            if (!vorgegeben)
                wahl.Clear();
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (!Plays(player.Id) || wahl.Accept(player, payload) is not { } gewaehlt)
                return null;

            telefonGewaehlt = true;
            return new WheelTargetChosen(gewaehlt.ChooserId, gewaehlt.ChosenId);
        }

        public bool HasSubmitted(Guid playerId) => telefonGewaehlt && playerId == wahl.Chooser;

        public void CloseRound()
        {
            laeuft = false;
            gezaehlt = true;
        }

        public void DiscardRunning() => laeuft = false;

        public void Clear()
        {
            segment = null;
            laeuft = false;
            gezaehlt = false;
            telefonGewaehlt = false;
            vorgegeben = false;
            wahl.Clear();
        }

        public bool SetPair(Guid chooserId, Guid? partnerId)
        {
            if (!wahl.SetPair(chooserId, partnerId))
                return false;

            vorgegeben = partnerId != null;
            telefonGewaehlt = false;
            return true;
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var waehlt = player != null && phase == MinigamePhase.Running && Plays(player.Id) && !HasSubmitted(player.Id);

            // Das Feld sieht vor der Aufloesung nur, wer jetzt waehlt - sonst verriete das Telefon den Beamer.
            return new WheelClientInfo
            {
                ChoosesTarget = waehlt,
                Options = waehlt ? wahl.OptionsFor(player) : new(),
                ChoicePrompt = waehlt ? GluecksradEvaluator.Zielfrage(segment!.Value) : null,
                SegmentName = (waehlt || gezaehlt) && segment is { } s ? GluecksradEvaluator.Name(s) : null,
                ResultText = gezaehlt && segment is { } t
                    ? GluecksradEvaluator.Ausgang(new(t, wahl.Chooser, wahl.Partner), id => kontext.NameVon(id) ?? "?")
                    : null,
            };
        }
    }
}
