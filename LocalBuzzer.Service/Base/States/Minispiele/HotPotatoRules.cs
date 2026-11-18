using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon bei der Heissen Kartoffel abgibt: ein Urteil oder ein Ziel, nie beides. Feldnamen = <c>kartoffel.js</c>.</summary>
    public sealed class HotPotatoPayload
    {
        /// <summary>Die Nummer der Aussage, die das Telefon zeigte - eine andere als die laufende wird verworfen.</summary>
        public int StatementNumber { get; set; }

        /// <summary>Das Urteil: <c>true</c> = wahr.</summary>
        public bool? Answer { get; set; }

        /// <summary>An wen der Halter weitergibt - nur nach einer richtigen Antwort.</summary>
        public Guid? PassTo { get; set; }
    }

    /// <summary>Eine Aussage der Aufloesung - erst nach der Runde, fuer alle.</summary>
    public sealed class HotPotatoStatementInfo
    {
        public string Text { get; set; } = string.Empty;

        public bool IsTrue { get; set; }
    }

    /// <summary>
    /// Was ein Telefon bei der Heissen Kartoffel sieht. Wer haelt, sehen alle; Aussage und Wahl nur der Halter. <b>Kein Feld
    /// traegt den Zuender oder eine Restzeit</b>, und die Wahrheit einer Aussage steht erst nach der Runde da.
    /// </summary>
    public sealed class HotPotatoClientInfo
    {
        /// <summary>Wer die Kartoffel haelt - nach dem Knall, bei wem sie geplatzt ist.</summary>
        public string? HolderName { get; set; }

        /// <summary>Wer sie dem Halter gegeben hat; <c>null</c> beim Starthalter.</summary>
        public string? PasserName { get; set; }

        public bool IsHolder { get; set; }

        /// <summary>Die laufende Aussage des Halters, ab 1 - das Telefon schickt sie mit; 0 fuer alle anderen.</summary>
        public int StatementNumber { get; set; }

        /// <summary>Der Text der laufenden Aussage - nur fuer den Halter, solange er urteilt.</summary>
        public string? StatementText { get; set; }

        /// <summary>Die letzte Antwort des Halters lag daneben - die naechste Aussage steht schon da.</summary>
        public bool LastWrong { get; set; }

        /// <summary>Der Halter lag richtig und waehlt, an wen er weitergibt.</summary>
        public bool Choosing { get; set; }

        /// <summary>Die Ziele - nur fuer den Halter, waehrend er waehlt.</summary>
        public List<MinigamePlayerOption> Options { get; set; } = new();

        /// <summary>Erst nach der Runde: ob sie geplatzt ist (sonst hat der Spielleiter geschlossen).</summary>
        public bool Exploded { get; set; }

        /// <summary>Erst nach dem Knall: nach so vielen Sekunden.</summary>
        public int? FuseSeconds { get; set; }

        /// <summary>Erst nach der Runde: die gezeigten Aussagen samt Wahrheit.</summary>
        public List<HotPotatoStatementInfo> Resolution { get; set; } = new();
    }

    /// <summary>
    /// Die Heisse Kartoffel am Server (M35 C6). Zuerst haelt sie der Ausloeser, sonst legt der Spielleiter fest (E1). Der
    /// Halter beurteilt Aussagen aus den Zeilen (je Runde gemischt, reihum); nach einer richtigen gibt er per Spielerwahl
    /// weiter, nach einer falschen kommt die naechste. <b>Der Zuender bleibt hier</b> - kein Zustand traegt ihn oder eine
    /// Restzeit; es knallt ueber die Fristwache. <b>Nur der Halter</b> kann antworten und weitergeben - das prueft dieser
    /// Server. Gesperrt wird dafuer niemand: ein Halterwechsel aenderte sonst die Sperre am Telefon, und der Rahmen startete
    /// dort den Countdown neu.
    /// </summary>
    public sealed class HotPotatoRules : IMinigamePairRules, IMinigameDeadlineRules
    {
        private readonly MinigameRulesContext kontext;
        private readonly TimeSpan vorlauf;
        private readonly int laengsteSekunden;
        private readonly IReadOnlyList<MinispielZeile> aussagen;
        private Guid? erster;
        private Guid? halter;
        private Guid? zuschieber;
        private TimeSpan start;
        private TimeSpan zuender;
        private bool laeuft;
        private bool aufgeloest;
        private bool geknallt;
        private bool darfWeitergeben;
        private bool letzteFalsch;
        private int nummer;
        private int zeiger;
        private int[] reihenfolge = [];
        private List<KartoffelZug> verlauf = new();
        private Dictionary<Guid, SlidingRateLimit> takt = new();

        public HotPotatoRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            this.kontext = kontext;
            vorlauf = TimeSpan.FromSeconds(Math.Max(0, setup.CountdownSeconds));
            laengsteSekunden = setup.DurationSeconds;
            aussagen = setup.Content ?? [];
            erster = IstMitspieler(setup.TriggerPlayerId) ? setup.TriggerPlayerId : null;
        }

        public MinigameKind Kind => MinigameKind.HotPotato;

        /// <summary>Wer haelt - vor dem Start, wer zuerst halten wird.</summary>
        public Guid? ChooserId => halter ?? erster;

        /// <summary>Ein Gegenueber gibt es nicht.</summary>
        public Guid? PartnerId => null;

        /// <summary>Fuer den Spielleiter, erst nach der Runde - vorher verriete sie Zuender und Wahrheit.</summary>
        public KartoffelRunde? Auswertung => aufgeloest ? new(Knall, verlauf.ToList(), geknallt ? Sekunden(zuender) : null) : null;

        private KartoffelKnall? Knall => geknallt && halter is { } h ? new(h, zuschieber) : null;

        /// <summary>Beim Knall der Halter samt Zuschieber - sonst nichts.</summary>
        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => IsDue && halter is { } h ? new Dictionary<Guid, object> { [h] = new KartoffelKnall(h, zuschieber) } : new Dictionary<Guid, object>();

        private TimeSpan Laufzeit => kontext.Jetzt() - start;

        public bool IsDue => laeuft && Laufzeit >= zuender;

        private MinispielZeile? Aktuelle => reihenfolge.Length > 0 ? aussagen[reihenfolge[zeiger]] : null;

        public void StartRound(int round)
        {
            var (kuerzeste, laengste) = KartoffelEvaluator.Zuendbereich(laengsteSekunden);
            var spanne = (int)(laengste - kuerzeste).TotalMilliseconds;
            zuender = kuerzeste + TimeSpan.FromMilliseconds(Math.Clamp(kontext.Zufall(0, spanne + 1), 0, spanne));

            reihenfolge = Shuffle.Order(aussagen.Count, kontext.Zufall);
            zeiger = 0;
            nummer = 1;
            start = kontext.Jetzt() + vorlauf;
            halter = erster;
            zuschieber = null;
            darfWeitergeben = false;
            letzteFalsch = false;
            verlauf = new();
            takt = new();
            laeuft = true;
            aufgeloest = false;
            geknallt = false;
            Zeige();
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (MinigamePayload.Read<HotPotatoPayload>(payload) is not { } abgabe || !laeuft || halter != player.Id)
                return null;

            var jetzt = kontext.Jetzt();
            var zeit = jetzt - start;

            // Im Vorlauf steht noch keine Aussage; ab dem Knall zaehlt nichts mehr, auch bevor die Fristwache schliesst.
            // Eine andere Nummer ist alt (doppelt gesendet, vor dem Weitergeben) oder gefaelscht.
            if (zeit < TimeSpan.Zero || zeit >= zuender || abgabe.StatementNumber != nummer)
                return null;

            return (abgabe.Answer, abgabe.PassTo) switch
            {
                ({ } urteil, null) => Urteile(player.Id, urteil, jetzt),
                (null, { } ziel) => GibWeiter(player.Id, ziel),
                _ => null,
            };
        }

        /// <summary>Niemand gibt „ab": die Runde schliesst allein der Knall (oder der Spielleiter).</summary>
        public bool HasSubmitted(Guid playerId) => false;

        public void CloseRound()
        {
            geknallt = IsDue;
            laeuft = false;
            aufgeloest = true;
        }

        /// <summary>Ein Abbruch verwirft nur eine laufende Runde - eine schon geschlossene behält Knall und Auflösung (V25, wie die Staffel).</summary>
        public void DiscardRunning()
        {
            if (!laeuft)
                return;

            laeuft = false;
            aufgeloest = false;
            geknallt = false;
        }

        public void Clear()
        {
            halter = null;
            zuschieber = null;
            laeuft = false;
            aufgeloest = false;
            geknallt = false;
            darfWeitergeben = false;
            letzteFalsch = false;
            nummer = 0;
            verlauf = new();
            takt = new();
        }

        /// <summary>
        /// Der Spielleiter legt fest, wer haelt (E1); ein Gegenueber gibt es nicht. Mitten in der Runde legt er sie dort hin -
        /// zugeschoben hat sie dann niemand, und der neue Halter bekommt eine neue Aussage.
        /// </summary>
        public bool SetPair(Guid chooserId, Guid? partnerId)
        {
            if (partnerId != null || !IstMitspieler(chooserId))
                return false;

            erster = chooserId;

            if (!laeuft || chooserId == halter)
                return true;

            // Eine Aussage, die der bisherige Halter nicht mehr beantworten kann, gehoert nicht in die Aufloesung.
            if (verlauf.Count > 0 && verlauf[^1].Richtig == null)
                verlauf.RemoveAt(verlauf.Count - 1);

            halter = chooserId;
            zuschieber = null;
            darfWeitergeben = false;
            letzteFalsch = false;
            NaechsteAussage();
            return true;
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var info = new HotPotatoClientInfo
            {
                HolderName = halter is { } h ? Name(h) : null,
                PasserName = zuschieber is { } z ? Name(z) : null,
            };

            // Erst wenn niemand mehr antworten kann, sind Zuender und Wahrheit kein Geheimnis mehr.
            if (aufgeloest && !laeuft && phase is MinigamePhase.RoundOver or MinigamePhase.Finished)
            {
                info.Exploded = geknallt;
                info.FuseSeconds = geknallt ? Sekunden(zuender) : null;
                info.Resolution = verlauf.Select(z => new HotPotatoStatementInfo { Text = z.Aussage, IsTrue = z.Wahr }).ToList();
            }

            if (player == null)
                return info;

            info.IsHolder = player.Id == halter;

            if (!info.IsHolder || !laeuft || phase != MinigamePhase.Running)
                return info;

            info.StatementNumber = nummer;

            if (darfWeitergeben)
            {
                info.Choosing = true;
                info.Options = Ziele(player.Id)
                    .Select(p => new MinigamePlayerOption { Id = p.Id, Name = Name(p.Id) })
                    .OrderBy(o => o.Name, StringComparer.CurrentCulture)
                    .ToList();
            }
            else
            {
                info.StatementText = Aktuelle?.Text;
                info.LastWrong = letzteFalsch;
            }

            return info;
        }

        private object? Urteile(Guid spieler, bool urteil, TimeSpan jetzt)
        {
            if (darfWeitergeben || Aktuelle is not { } aussage || verlauf.Count == 0)
                return null;

            if (!takt.TryGetValue(spieler, out var grenze))
                takt[spieler] = grenze = new(KartoffelEvaluator.MaxAntwortenJeFenster, TimeSpan.FromSeconds(KartoffelEvaluator.FensterSekunden));

            if (!grenze.TryTake(jetzt))
                return null;

            var richtig = urteil == aussage.Wahr;
            var antwort = new KartoffelAntwort(nummer, richtig);
            verlauf[^1] = verlauf[^1] with { Richtig = richtig };
            letzteFalsch = !richtig;

            if (richtig)
                darfWeitergeben = true;
            else
                NaechsteAussage();

            return antwort;
        }

        private object? GibWeiter(Guid spieler, Guid ziel)
        {
            if (!darfWeitergeben || Ziele(spieler).All(p => p.Id != ziel))
                return null;

            verlauf[^1] = verlauf[^1] with { WeiterAn = ziel };
            zuschieber = spieler;
            halter = ziel;
            darfWeitergeben = false;
            letzteFalsch = false;
            NaechsteAussage();

            // Wer haelt, sehen alle - jedes Telefon bekommt seinen Zustand, ohne neuen Durchgang.
            return new MinigameSeenByAll(new KartoffelWeitergabe(spieler, ziel));
        }

        /// <summary>
        /// Wohin der Halter weitergeben darf: verbundene Mitspieler ausser ihm selbst, im Teamspiel nur Gegner (M37 T8). Direkt
        /// zurueck an den, der sie ihm gab, nur wenn sonst niemand da ist (Annahme C6b) - sonst spielten zwei sie sich zu.
        /// </summary>
        private List<Player> Ziele(Guid spieler)
        {
            var verbunden = kontext.Mitspieler().Where(p => kontext.IstZiel(spieler, p.Id) && p.ConnectionState == PlayerConnection.Connected).ToList();
            var ohneRueckgabe = verbunden.Where(p => p.Id != zuschieber).ToList();
            return ohneRueckgabe.Count > 0 ? ohneRueckgabe : verbunden;
        }

        /// <summary>Die naechste Aussage; sind alle durch, beginnt eine neu gemischte Folge.</summary>
        private void NaechsteAussage()
        {
            nummer++;

            if (reihenfolge.Length == 0)
                return;

            if (++zeiger >= reihenfolge.Length)
            {
                reihenfolge = Shuffle.Order(aussagen.Count, kontext.Zufall);
                zeiger = 0;
            }

            Zeige();
        }

        /// <summary>Haelt fest, welche Aussage der Halter jetzt vor sich hat - fuer die Aufloesung.</summary>
        private void Zeige()
        {
            if (halter is { } h && Aktuelle is { } aussage)
                verlauf.Add(new KartoffelZug(h, aussage.Text, aussage.Wahr));
        }

        private static int Sekunden(TimeSpan zeit) => (int)Math.Round(zeit.TotalSeconds, MidpointRounding.AwayFromZero);

        private bool IstMitspieler(Guid? id) => id is { } gesucht && kontext.Mitspieler().Any(p => p.Id == gesucht);

        private string Name(Guid id) => kontext.NameVon(id) ?? "?";
    }
}
