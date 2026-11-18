using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Teams;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon des Läufers in der Staffel abgibt. Feldnamen = <c>staffel.js</c>; wer abgibt und für welches Team, sagt die Verbindung.</summary>
    public sealed class RelayPayload
    {
        /// <summary>Der Zug, den das Telefon sah (<see cref="StaffelStand.Zug"/>) - ein anderer wird verworfen.</summary>
        public int Turn { get; set; }

        public string? Answer { get; set; }
    }

    /// <summary>Ein Team in der Staffel, wie das Telefon es sieht: Name und Symbol nur als Text, die Farbe nur als Platz der Palette.</summary>
    public sealed class RelayTeamInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Symbol { get; set; } = string.Empty;

        public int ColorIndex { get; set; } = -1;

        /// <summary>So viele Teile hat das Team gelöst.</summary>
        public int Solved { get; set; }

        public bool IsOwn { get; set; }

        /// <summary>Ob das Team gerade läuft.</summary>
        public bool IsOnTurn { get; set; }

        /// <summary>Erst nach der Runde: ob das Team gewonnen hat.</summary>
        public bool IsWinner { get; set; }
    }

    /// <summary>Ein Teil der Auflösung - erst nach der Runde, für alle.</summary>
    public sealed class RelayPartInfo
    {
        public int Number { get; set; }

        public string Solution { get; set; } = string.Empty;

        /// <summary>„✚ Rot“, wer ihn gelöst hat; <c>null</c>: verfehlt.</summary>
        public string? Team { get; set; }
    }

    /// <summary>
    /// Was ein Telefon in der Staffel sieht: wer läuft, die Teams mit ihren Teilen, was zuletzt geschah. <b>Kein Feld trägt eine
    /// Lösung oder eine Antwort</b>, bevor die Runde zu ist; den Zug bekommt nur der Läufer.
    /// </summary>
    public sealed class RelayClientInfo
    {
        public int TotalParts { get; set; }

        /// <summary>Der laufende Teil, 1-basiert; 0, wenn keiner läuft.</summary>
        public int Part { get; set; }

        /// <summary>Nur für den Läufer: der Zug, den sein Telefon mitschickt; sonst 0.</summary>
        public int Turn { get; set; }

        public bool IsRunner { get; set; }

        public string? RunnerName { get; set; }

        /// <summary>„✚ Rot“ - das Team am Zug.</summary>
        public string? RunnerTeam { get; set; }

        /// <summary>Der Teil des letzten Versuchs, 1-basiert; 0 vor dem ersten.</summary>
        public int LastPart { get; set; }

        public bool LastCorrect { get; set; }

        /// <summary>Die Spielleitung hat für das Team weitergegeben.</summary>
        public bool LastPassed { get; set; }

        /// <summary>Die Spielleitung hat die abgelehnte Antwort gelten lassen.</summary>
        public bool LastAccepted { get; set; }

        public string? LastTeam { get; set; }

        public List<RelayTeamInfo> Teams { get; set; } = new();

        /// <summary>Erst nach der Runde: jeder Teil mit Lösung und wer ihn hatte.</summary>
        public List<RelayPartInfo> Resolution { get; set; } = new();

        /// <summary>Erst nach der Runde: ob das eigene Team gewonnen hat.</summary>
        public bool IsWinner { get; set; }
    }

    /// <summary>
    /// Die Staffel am Server (M37 T11). Wer mitläuft und für welches Team, hält der Start der Runde aus der Aufstellung des Hubs fest -
    /// verbundene Mitglieder mit Team, nie aus der Nachricht; die anderen schauen zu. Je Team laufen die Mitglieder in Sprecherfolge,
    /// beginnend beim Sprecher. <b>Nur der Läufer kann antworten, nur auf seinen Zug</b> - das prüft diese Regel selbst; gesperrt wird
    /// dafür niemand, ein Läuferwechsel änderte sonst die Sperre am Telefon, und der Rahmen startete dort den Countdown neu (wie bei
    /// der Kartoffel). Die Lösungen bleiben hier bis zur Auflösung. Die Spielleitung greift über <see cref="MinigameState.Lenke{T}"/>
    /// ein: abgelehnte Antwort gelten lassen, für das Team am Zug weitergeben (E1).
    /// </summary>
    public sealed class RelayRules : IMinigameRules
    {
        private readonly MinigameRulesContext kontext;
        private readonly IReadOnlyList<MinispielZeile> teile;
        private readonly Guid? ausloeser;
        private Dictionary<Guid, Guid> teilnehmer = new();
        private Dictionary<Guid, TeamRosterEntry> teams = new();
        private Dictionary<Guid, SlidingRateLimit> takt = new();
        private StaffelStand stand = StaffelStand.Leer;
        private bool laeuft;
        private bool aufgeloest;

        public RelayRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            this.kontext = kontext;
            teile = setup.Content ?? [];
            ausloeser = setup.TriggerPlayerId;
        }

        public MinigameKind Kind => MinigameKind.Relay;

        /// <summary>Der Stand für Fragefenster und Beamer - gelesen unter der Sperre des Rahmens.</summary>
        public StaffelStand Stand => stand;

        /// <summary>Ob die Staffel geschlossen und gewertet ist - nicht abgebrochen.</summary>
        public bool Aufgeloest => aufgeloest && !laeuft;

        /// <summary>Wer beim Start mitlief, je Spieler sein Team.</summary>
        public IReadOnlyDictionary<Guid, Guid> Teilnehmer => teilnehmer;

        /// <summary>Ob die Spielleitung die zuletzt abgelehnte Antwort gelten lassen kann - laufend oder nach dem Schluss, nie nach dem Abbruch.</summary>
        public bool KannGelten => (laeuft || aufgeloest) && stand.VorDemFehler != null;

        /// <summary>Ob die Spielleitung für das Team am Zug weitergeben kann - nur in der laufenden Runde.</summary>
        public bool KannWeitergeben => laeuft && !stand.Fertig;

        /// <summary>Gewertet wird aus dem Stand, nicht aus Abgaben je Spieler.</summary>
        public IReadOnlyDictionary<Guid, object> CurrentSubmissions => new Dictionary<Guid, object>();

        public bool Plays(Guid playerId) => teilnehmer.ContainsKey(playerId);

        public string? WatcherText => "Beim Start warst du in keinem Team oder nicht verbunden – diesmal läufst du nicht mit.";

        /// <summary>Niemand gibt „ab“ - erst wenn alle Teile durch sind, gilt es für alle; dann schließt der Rahmen die Runde.</summary>
        public bool HasSubmitted(Guid playerId) => laeuft && stand.Fertig;

        public void StartRound(int round)
        {
            teilnehmer = new();
            teams = new();

            foreach (var spieler in kontext.Mitspieler().Where(p => p.ConnectionState == PlayerConnection.Connected))
            {
                // Ohne Team läuft niemand (fail-closed) - auch nicht ohne Teamspiel, dann ist die Aufstellung leer.
                if (kontext.Team?.Invoke(spieler.Id) is not { } eintrag)
                    continue;

                teilnehmer[spieler.Id] = eintrag.TeamId;
                teams[eintrag.TeamId] = eintrag;
            }

            var reihe = teams.Values
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.TeamId)
                .Select(t => new StaffelTeam(t.TeamId, Laeuferfolge(t)))
                .ToList();

            Guid? start = ausloeser is { } wer && teilnehmer.TryGetValue(wer, out var team) ? team : null;

            stand = StaffelEvaluator.Beginne(reihe, teile.Count, start, Erreichbar);
            takt = new();
            laeuft = true;
            aufgeloest = false;
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (!laeuft || MinigamePayload.Read<RelayPayload>(payload) is not { } abgabe)
                return null;

            // Nur der Läufer, nur auf seinen Zug - die Regel prüft selbst, nicht nur der Rahmen über Plays.
            if (stand.TeamAmZug is not { } team || stand.Laeufer != player.Id || abgabe.Turn != stand.Zug)
                return null;

            var antwort = BuzzerInputState.Kuerze(abgabe.Answer?.Trim());

            if (antwort.Length == 0)
                return null;

            if (!takt.TryGetValue(player.Id, out var grenze))
                takt[player.Id] = grenze = new(StaffelEvaluator.MaxAntwortenJeFenster, TimeSpan.FromSeconds(StaffelEvaluator.FensterSekunden));

            if (!grenze.TryTake(kontext.Jetzt()))
                return null;

            var teil = stand.Teil;
            var richtig = BuchstabensalatBegriffe.Gleich(antwort, teile[teil].Text);
            stand = StaffelEvaluator.Antworte(stand, richtig, Erreichbar);

            // Wer läuft, sehen alle - jedes Telefon bekommt seinen Zustand, ohne neuen Durchgang.
            return new MinigameSeenByAll(new StaffelAntwort(teil + 1, team, antwort, richtig));
        }

        /// <summary>Die Spielleitung lässt die zuletzt abgelehnte Antwort gelten (E1); <c>false</c>, wenn es keine gibt.</summary>
        public bool LasseGelten()
        {
            if (!KannGelten || StaffelEvaluator.LasseGelten(stand, Erreichbar) is not { } neu)
                return false;

            stand = neu;
            return true;
        }

        /// <summary>Die Spielleitung gibt für das Team am Zug weiter (E1) - etwa, wenn das Telefon des Läufers fehlt.</summary>
        public bool GibWeiter()
        {
            if (!KannWeitergeben)
                return false;

            stand = StaffelEvaluator.GibWeiter(stand, Erreichbar);
            return true;
        }

        public void CloseRound()
        {
            laeuft = false;
            aufgeloest = true;
        }

        /// <summary>Ein Abbruch verwirft nur eine laufende Staffel - eine schon geschlossene bleibt gewertet, samt Auflösung.</summary>
        public void DiscardRunning()
        {
            if (!laeuft)
                return;

            laeuft = false;
            aufgeloest = false;
        }

        public void Clear()
        {
            teilnehmer = new();
            teams = new();
            takt = new();
            stand = StaffelStand.Leer;
            laeuft = false;
            aufgeloest = false;
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            Guid? eigenes = player != null && teilnehmer.TryGetValue(player.Id, out var team) ? team : null;
            var ende = aufgeloest && !laeuft && phase is MinigamePhase.RoundOver or MinigamePhase.Finished;
            var ergebnis = ende ? StaffelEvaluator.Werte(stand) : StaffelErgebnis.Leer;

            var info = new RelayClientInfo
            {
                TotalParts = stand.Teile,
                Part = laeuft && !stand.Fertig ? stand.Teil + 1 : 0,
                RunnerName = laeuft && stand.Laeufer is { } wer ? Name(wer) : null,
                RunnerTeam = laeuft && stand.TeamAmZug is { } amZug ? Titel(amZug) : null,
                Teams = stand.Teams.Select(t => TeamInfo(t.TeamId, eigenes, ergebnis)).ToList(),
                IsWinner = eigenes is { } id && ergebnis.IstGewinner(id),
            };

            if (stand.Letzter is { } letzter)
            {
                info.LastPart = letzter.Teil;
                info.LastCorrect = letzter.Richtig;
                info.LastPassed = letzter.Gepasst;
                info.LastAccepted = letzter.Gegolten;
                info.LastTeam = Titel(letzter.TeamId);
            }

            // Erst wenn niemand mehr antwortet, gehen die Lösungen hinaus - vorher kennt sie nur der Server.
            if (ende)
            {
                info.Resolution = teile.Take(stand.Teile)
                    .Select((z, i) => new RelayPartInfo { Number = i + 1, Solution = z.Text, Team = stand.Geloest[i] is { } g ? Titel(g) : null })
                    .ToList();
            }

            if (player != null && laeuft && phase == MinigamePhase.Running && stand.Laeufer == player.Id)
            {
                info.IsRunner = true;
                info.Turn = stand.Zug;
            }

            return info;
        }

        /// <summary>Die Mitglieder des Teams, die beim Start dabei waren, in Sprecherfolge - beginnend beim Sprecher des Teams.</summary>
        private IReadOnlyList<Guid> Laeuferfolge(TeamRosterEntry team)
        {
            var mitglieder = teilnehmer.Where(t => t.Value == team.TeamId).Select(t => new Teammitglied(t.Key, Name(t.Key))).ToList();
            // Über die Kennung: zwei Mitglieder können denselben Namen tragen (V27).
            var sprecher = Sprecherfolge.Folge(mitglieder, 0).ToList().FindIndex(id => id == team.SpeakerId);

            return Sprecherfolge.Folge(mitglieder, Math.Max(0, sprecher));
        }

        private RelayTeamInfo TeamInfo(Guid teamId, Guid? eigenes, StaffelErgebnis ergebnis)
        {
            var eintrag = teams.GetValueOrDefault(teamId);

            return new RelayTeamInfo
            {
                Name = eintrag?.Name ?? "?",
                Symbol = eintrag?.Symbol ?? string.Empty,
                ColorIndex = eintrag?.ColorIndex ?? -1,
                Solved = stand.GeloestVon(teamId),
                IsOwn = teamId == eigenes,
                IsOnTurn = laeuft && stand.TeamAmZug == teamId,
                IsWinner = ergebnis.IstGewinner(teamId),
            };
        }

        private bool Erreichbar(Guid id) => kontext.Mitspieler().Any(p => p.Id == id && p.ConnectionState == PlayerConnection.Connected);

        /// <summary>„✚ Rot“ - Symbol und Name, nie Farbe allein.</summary>
        private string Titel(Guid teamId) => teams.GetValueOrDefault(teamId) is { } t ? $"{t.Symbol} {t.Name}".Trim() : "?";

        private string Name(Guid id) => kontext.NameVon(id) ?? "?";
    }
}
