using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon bei Teilen oder Nehmen abgibt. Feldname = <c>teilen.js</c>.</summary>
    public sealed class ShareOrTakePayload
    {
        /// <summary><c>true</c> nimmt, <c>false</c> teilt; fehlt es, ist die Abgabe kaputt.</summary>
        public bool? Take { get; set; }
    }

    /// <summary>Was ein Telefon bei Teilen oder Nehmen sieht - die Wahl des anderen erst nach der Aufloesung.</summary>
    public sealed class ShareOrTakeClientInfo
    {
        /// <summary>Dieses Telefon waehlt gerade den Partner.</summary>
        public bool ChoosesPartner { get; set; }

        public List<MinigamePlayerOption> Options { get; set; } = new();

        public string? ChooserName { get; set; }

        public string? PartnerName { get; set; }

        /// <summary>Ob dieses Telefon zum Paar gehoert - sonst schaut es zu.</summary>
        public bool IsPlayer { get; set; }

        public bool? OwnTake { get; set; }

        /// <summary>Die Wahl des anderen - erst nach dem Schliessen.</summary>
        public bool? OtherTake { get; set; }
    }

    /// <summary>
    /// Teilen oder Nehmen am Server (M35 C2): der Ausloeser waehlt einen Partner (<see cref="PartnerChoice"/>),
    /// dann entscheiden beide geheim. Gewertet wird im Fragefenster (<c>TeilenNehmenEvaluator</c>).
    /// </summary>
    public sealed class ShareOrTakeRules : IMinigamePairRules
    {
        private readonly PartnerChoice wahl;
        private Dictionary<Guid, bool> laufend = new();
        private Dictionary<Guid, bool> gezaehlt = new();

        public ShareOrTakeRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            wahl = new PartnerChoice(setup.TriggerPlayerId, "Partner", kontext);
        }

        public MinigameKind Kind => MinigameKind.ShareOrTake;

        public Guid? ChooserId => wahl.Chooser;

        public Guid? PartnerId => wahl.Partner;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => laufend.ToDictionary(w => w.Key, w => (object)w.Value);

        public bool Plays(Guid playerId) => wahl.Plays(playerId);

        public string? WatcherText => wahl.WatcherText;

        public void StartRound(int round)
        {
            laufend = new();
            gezaehlt = new();
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (!wahl.Chosen)
                return wahl.Accept(player, payload);

            if (MinigamePayload.Read<ShareOrTakePayload>(payload)?.Take is not { } nimmt)
                return null;

            laufend[player.Id] = nimmt;
            return nimmt;
        }

        public bool HasSubmitted(Guid playerId) => wahl.Chosen && laufend.ContainsKey(playerId);

        public void CloseRound() => gezaehlt = new(laufend);

        public void DiscardRunning() => laufend = new();

        public void Clear()
        {
            laufend = new();
            gezaehlt = new();
            wahl.Clear();
        }

        public bool SetPair(Guid chooserId, Guid? partnerId)
        {
            if (!wahl.SetPair(chooserId, partnerId))
                return false;

            DiscardRunning();
            return true;
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var info = new ShareOrTakeClientInfo
            {
                ChooserName = wahl.ChooserName,
                PartnerName = wahl.PartnerName,
                ChoosesPartner = player != null && phase == MinigamePhase.Running && wahl.ChoosesNow(player.Id),
                Options = phase == MinigamePhase.Running ? wahl.OptionsFor(player) : new(),
            };

            if (player == null || wahl is not { Chooser: { } a, Partner: { } b } || !wahl.Plays(player.Id))
                return info;

            var anderer = player.Id == a ? b : a;
            var gezeigt = phase == MinigamePhase.Running ? laufend : gezaehlt;

            info.IsPlayer = true;
            info.OwnTake = gezeigt.TryGetValue(player.Id, out var eigene) ? eigene : null;

            // Geheim bis zur Aufloesung: solange die Runde laeuft, nie die fremde Wahl.
            info.OtherTake = phase != MinigamePhase.Running && gezeigt.TryGetValue(anderer, out var fremde) ? fremde : null;

            return info;
        }
    }
}
