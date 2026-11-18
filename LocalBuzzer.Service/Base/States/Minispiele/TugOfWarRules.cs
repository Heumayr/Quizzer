using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon im Tauziehen meldet - im Takt und am Schluss. Feldnamen = <c>tauziehen.js</c>; eine Zeit gehört nicht hinein.</summary>
    public sealed class TugOfWarPayload
    {
        public int Taps { get; set; }

        /// <summary>Der Endstand - danach nimmt der Server von diesem Telefon nichts mehr an.</summary>
        public bool IsFinal { get; set; }
    }

    /// <summary>Ein Team am Seil, wie das Telefon es sieht: Name und Symbol nur als Text, die Farbe nur als Platz der Palette.</summary>
    public sealed class TugOfWarTeamInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Symbol { get; set; } = string.Empty;

        public int ColorIndex { get; set; } = -1;

        /// <summary>Wie stark das Team zieht - „42 Tipps“ bzw. „Ø 10,5 Tipps“.</summary>
        public string Pull { get; set; } = string.Empty;

        /// <summary>Ob es das Team dieses Telefons ist.</summary>
        public bool IsOwn { get; set; }

        /// <summary>Erst am Ende: ob das Team gewonnen hat.</summary>
        public bool IsWinner { get; set; }
    }

    /// <summary>Was ein Telefon im Tauziehen sieht: seinen Stand, die Teams am Seil, am Ende den Ausgang.</summary>
    public sealed class TugOfWarClientInfo
    {
        public int DurationSeconds { get; set; }

        /// <summary>Dieselbe Grenze wie am Server - das Telefon zählt nicht mehr, als angenommen wird.</summary>
        public int MaxTapsPerSecond { get; set; }

        public int ReportIntervalMs { get; set; }

        /// <summary>Der zuletzt angenommene eigene Stand - nach dem Wiederverbinden geht es dort weiter.</summary>
        public int? OwnTaps { get; set; }

        /// <summary>So lange wird an der Serveruhr schon gezogen, höchstens die Dauer - nach dem Wiederverbinden zählt nur der Rest.</summary>
        public int ElapsedMs { get; set; }

        /// <summary>Wo das Seil steht, nur bei zwei Teams: −1 beim ersten, +1 beim zweiten (<see cref="TauziehenErgebnis.Seil"/>).</summary>
        public double? Rope { get; set; }

        public List<TugOfWarTeamInfo> Teams { get; set; } = new();

        /// <summary>Erst am Ende: ob das eigene Team gewonnen hat.</summary>
        public bool IsWinner { get; set; }
    }

    /// <summary>
    /// Das Tauziehen am Server (M37 T9): Team gegen Team, gezählt am Telefon, <b>entschieden an der Serveruhr</b>. Wer mitzieht
    /// und für welches Team, hält der Start der Runde fest - aus der Aufstellung des Hubs, nie aus der Nachricht; wer dann ohne
    /// Team oder nicht verbunden ist, schaut zu. Die Laufzeit misst der Server ab dem Ende des Countdowns, ein Stand gilt nur,
    /// wenn er dazu passt (<see cref="TauziehenEvaluator.Pruefe"/>). Die Runde schließt die Fristwache nach Dauer und Nachlauf -
    /// oder früher, wenn alle ihren Endstand geschickt haben. Gewertet wird im Fragefenster.
    /// </summary>
    public sealed class TugOfWarRules : IMinigameDeadlineRules
    {
        private readonly MinigameRulesContext kontext;
        private readonly int dauer;
        private readonly TimeSpan vorlauf;
        private readonly TeamMinigameScoring wertung;
        private TimeSpan start;
        private bool laeuft;
        private Dictionary<Guid, Guid> teilnehmer = new();
        private Dictionary<Guid, TeamRosterEntry> teams = new();
        private Dictionary<Guid, TauziehenZug> laufend = new();
        private Dictionary<Guid, TauziehenZug> gezaehlt = new();

        public TugOfWarRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            this.kontext = kontext;
            dauer = setup.DurationSeconds;
            vorlauf = TimeSpan.FromSeconds(Math.Max(0, setup.CountdownSeconds));
            wertung = setup.TeamScoring;
        }

        public MinigameKind Kind => MinigameKind.TugOfWar;

        /// <summary>Wer beim Start mitzog, je Spieler sein Team.</summary>
        public IReadOnlyDictionary<Guid, Guid> Teilnehmer => teilnehmer;

        /// <summary>Der Stand am Seil: während der Runde der laufende, danach der gezählte. Gelesen unter der Sperre des Rahmens.</summary>
        public TauziehenErgebnis Stand => TauziehenEvaluator.Werte(laeuft ? Staende() : gezaehlt, wertung,
            team => teams.TryGetValue(team, out var eintrag) ? eintrag.SortOrder : int.MaxValue);

        /// <summary>Jeder Teilnehmer mit seinem Stand, auch ohne Tipp - er zählt im Durchschnitt seines Teams mit.</summary>
        public IReadOnlyDictionary<Guid, object> CurrentSubmissions => Staende().ToDictionary(e => e.Key, e => (object)e.Value);

        /// <summary>Seit wann gezogen wird; im Countdown negativ.</summary>
        private TimeSpan Laufzeit => kontext.Jetzt() - start;

        public bool IsDue => laeuft && Laufzeit > TauziehenEvaluator.Frist(dauer);

        public bool Plays(Guid playerId) => teilnehmer.ContainsKey(playerId);

        public string? WatcherText => "Beim Start warst du in keinem Team oder nicht verbunden – diesmal ziehst du nicht mit.";

        public void StartRound(int round)
        {
            start = kontext.Jetzt() + vorlauf;
            teilnehmer = new();
            teams = new();

            foreach (var spieler in kontext.Mitspieler().Where(p => p.ConnectionState == PlayerConnection.Connected))
            {
                // Ohne Team zieht niemand (fail-closed) - auch nicht ohne Teamspiel, dann ist die Aufstellung leer.
                if (kontext.Team?.Invoke(spieler.Id) is not { } eintrag)
                    continue;

                teilnehmer[spieler.Id] = eintrag.TeamId;
                teams[eintrag.TeamId] = eintrag;
            }

            laufend = new();
            gezaehlt = new();
            laeuft = true;
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (!laeuft || !teilnehmer.TryGetValue(player.Id, out var team) || MinigamePayload.Read<TugOfWarPayload>(payload) is not { } abgabe)
                return null;

            var zug = TauziehenEvaluator.Pruefe(
                laufend.GetValueOrDefault(player.Id), new TauziehenZug(team, abgabe.Taps, abgabe.IsFinal), Laufzeit, dauer);

            if (zug != null)
                laufend[player.Id] = zug;

            return zug;
        }

        public bool HasSubmitted(Guid playerId) => laufend.TryGetValue(playerId, out var zug) && zug.Ende;

        public void CloseRound()
        {
            gezaehlt = Staende();
            laeuft = false;
        }

        public void DiscardRunning()
        {
            laufend = new();
            laeuft = false;
        }

        public void Clear()
        {
            teilnehmer = new();
            teams = new();
            laufend = new();
            gezaehlt = new();
            laeuft = false;
        }

        /// <summary>Je Teilnehmer sein letzter Stand; wer nie gemeldet hat, mit 0 für sein Team.</summary>
        private Dictionary<Guid, TauziehenZug> Staende()
            => teilnehmer.ToDictionary(t => t.Key, t => laufend.GetValueOrDefault(t.Key) ?? new TauziehenZug(t.Value, 0, false));

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var ergebnis = Stand;
            var ende = phase == MinigamePhase.Finished;
            Guid? eigenes = player != null && teilnehmer.TryGetValue(player.Id, out var team) ? team : null;

            var info = new TugOfWarClientInfo
            {
                DurationSeconds = dauer,
                MaxTapsPerSecond = TauziehenEvaluator.MaxTapsPerSecond,
                ReportIntervalMs = TauziehenEvaluator.ReportIntervalMs,
                ElapsedMs = laeuft ? (int)Math.Clamp(Laufzeit.TotalMilliseconds, 0, dauer * 1000.0) : 0,
                Rope = ergebnis.Seil,
                Teams = ergebnis.Teams.Select(t => TeamInfo(t, t.TeamId == eigenes, ende && ergebnis.IstGewinner(t.TeamId))).ToList(),
                IsWinner = ende && eigenes is { } id && ergebnis.IstGewinner(id),
            };

            if (player != null && (laufend.GetValueOrDefault(player.Id) ?? gezaehlt.GetValueOrDefault(player.Id)) is { } zug)
                info.OwnTaps = zug.Tipps;

            return info;
        }

        private TugOfWarTeamInfo TeamInfo(TauziehenTeam team, bool eigenes, bool sieger)
        {
            var eintrag = teams.GetValueOrDefault(team.TeamId);

            return new TugOfWarTeamInfo
            {
                Name = eintrag?.Name ?? string.Empty,
                Symbol = eintrag?.Symbol ?? string.Empty,
                ColorIndex = eintrag?.ColorIndex ?? -1,
                Pull = team.ZugText,
                IsOwn = eigenes,
                IsWinner = sieger,
            };
        }
    }
}
