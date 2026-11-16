using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon beim Zocker abgibt. Feldname = <c>zocker.js</c>.</summary>
    public sealed class CrashPayload
    {
        /// <summary>Muss <c>true</c> sein - alles andere ist eine kaputte Abgabe.</summary>
        public bool? CashOut { get; set; }
    }

    /// <summary>Was ein Telefon beim Zocker sieht - den Absturzpunkt erst, wenn die Runde zu ist.</summary>
    public sealed class CrashClientInfo
    {
        /// <summary>Wo dieser Spieler ausgestiegen ist, in Hundertsteln - auch nach dem Wiederverbinden.</summary>
        public int? OwnMultiplier { get; set; }

        /// <summary>Der Absturzpunkt in Hundertsteln - <b>nie</b>, solange die Runde laeuft.</summary>
        public int? CrashMultiplier { get; set; }

        /// <summary>Erst nach der Runde: ob sie mit dem Absturz endete (sonst waren alle vorher draussen).</summary>
        public bool Crashed { get; set; }
    }

    /// <summary>Was der Beamer waehrend der Runde zeigt: nie der Absturzpunkt, bevor es abgestuerzt ist.</summary>
    /// <param name="Multiplikator">Der Stand an der Serveruhr in Hundertsteln; nach dem Absturz der Absturzpunkt.</param>
    /// <param name="Gestartet">Ob der Vorlauf (Countdown der Telefone) vorbei ist.</param>
    /// <param name="Abgestuerzt">Ob die Serveruhr den Absturz erreicht hat.</param>
    public sealed record CrashStand(int Multiplikator, bool Gestartet, bool Abgestuerzt);

    /// <summary>
    /// Der Zocker am Server (M35 C4): der Absturzpunkt wird je Runde gewuerfelt und bleibt hier; Telefon
    /// und Beamer erfahren ihn erst, wenn es abgestuerzt oder die Runde zu ist. Gewertet wird im
    /// Fragefenster (<see cref="ZockerEvaluator"/>).
    /// <para>
    /// <b>Ausstieg gegen Absturz entscheidet die Serveruhr beim Eintreffen der Abgabe</b>, nicht eine Zeit
    /// vom Telefon: die liesse sich faelschen, und wer den Absturz am Beamer gesehen hat, koennte mit einer
    /// frueheren Zeit noch aussteigen. Der Preis ist die Laufzeit im WLAN - fuer alle gleich, wie bei jedem
    /// Absturzspiel. Die Uhr laeuft erst nach dem Countdown der Telefone; davor nimmt der Server nichts an.
    /// </para>
    /// </summary>
    public sealed class CrashRules : IMinigameDeadlineRules
    {
        private readonly MinigameRulesContext kontext;
        private readonly TimeSpan vorlauf;
        private int absturz = ZockerEvaluator.Obergrenze;
        private TimeSpan start;
        private bool laeuft;
        private bool aufgedeckt;
        private bool abgestuerzt;
        private HashSet<Guid> teilnehmer = new();
        private Dictionary<Guid, int> laufend = new();
        private Dictionary<Guid, int> gezaehlt = new();

        public CrashRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            this.kontext = kontext;
            vorlauf = TimeSpan.FromSeconds(Math.Max(0, setup.CountdownSeconds));
        }

        public MinigameKind Kind => MinigameKind.Crash;

        /// <summary>Wer beim Start verbunden war - nur sie sind drin; wer spaeter kommt, schaut zu.</summary>
        public IReadOnlyCollection<Guid> Teilnehmer => teilnehmer;

        /// <summary>Der Absturzpunkt in Hundertsteln - <c>null</c>, solange die Runde laeuft.</summary>
        public int? Aufgedeckt => aufgedeckt ? absturz : null;

        /// <summary>Erst nach der Runde: ob sie mit dem Absturz endete.</summary>
        public bool Abgestuerzt => aufgedeckt && abgestuerzt;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => laufend.ToDictionary(a => a.Key, a => (object)new ZockerAusstieg(a.Value));

        /// <summary>Der Stand fuer den Beamer, an der Serveruhr gelesen.</summary>
        public CrashStand Stand
        {
            get
            {
                if (!laeuft)
                    return new(aufgedeckt ? absturz : ZockerEvaluator.Startwert, aufgedeckt, Abgestuerzt);

                var zeit = Laufzeit;
                return IstAbgestuerzt(zeit)
                    ? new(absturz, true, true)
                    : new(ZockerEvaluator.Multiplikator(zeit), zeit > TimeSpan.Zero, false);
            }
        }

        /// <summary>Seit wann der Multiplikator steigt; im Vorlauf negativ.</summary>
        private TimeSpan Laufzeit => kontext.Jetzt() - start;

        private bool IstAbgestuerzt(TimeSpan laufzeit) => laufzeit >= ZockerEvaluator.Absturzzeit(absturz);

        public bool IsDue => laeuft && IstAbgestuerzt(Laufzeit);

        public bool Plays(Guid playerId) => teilnehmer.Contains(playerId);

        public string? WatcherText => "Beim Start warst du nicht verbunden – diese Runde schaust du zu.";

        public void StartRound(int round)
        {
            absturz = ZockerEvaluator.Absturz(kontext.Zufall(0, ZockerEvaluator.Wurfbereich));
            start = kontext.Jetzt() + vorlauf;
            teilnehmer = kontext.Mitspieler().Where(p => p.ConnectionState == PlayerConnection.Connected).Select(p => p.Id).ToHashSet();
            laufend = new();
            gezaehlt = new();
            laeuft = true;
            aufgedeckt = false;
            abgestuerzt = false;
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (MinigamePayload.Read<CrashPayload>(payload)?.CashOut != true || !teilnehmer.Contains(player.Id))
                return null;

            var zeit = Laufzeit;

            // Im Vorlauf steht noch kein Multiplikator; ab dem Absturz zaehlt kein Ausstieg mehr.
            if (zeit < TimeSpan.Zero || IstAbgestuerzt(zeit))
                return null;

            var multiplikator = ZockerEvaluator.Multiplikator(zeit);
            laufend[player.Id] = multiplikator;
            return new ZockerAusstieg(multiplikator);
        }

        public bool HasSubmitted(Guid playerId) => laufend.ContainsKey(playerId);

        public void CloseRound()
        {
            abgestuerzt = laeuft && IstAbgestuerzt(Laufzeit);
            gezaehlt = new(laufend);
            laeuft = false;
            aufgedeckt = true;
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
            teilnehmer = new();
            laeuft = false;
            aufgedeckt = false;
            abgestuerzt = false;
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var info = new CrashClientInfo();

            if (player != null)
                info.OwnMultiplier = laufend.TryGetValue(player.Id, out var m) || gezaehlt.TryGetValue(player.Id, out m) ? m : null;

            // Geheim, bis die Runde zu ist - danach sieht jeder, wo es abgestuerzt ist (oder waere).
            if (aufgedeckt)
            {
                info.CrashMultiplier = absturz;
                info.Crashed = abgestuerzt;
            }

            return info;
        }
    }
}
