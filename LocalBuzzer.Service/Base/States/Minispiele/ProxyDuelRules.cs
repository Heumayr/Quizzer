using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Ein Team im Stellvertreter-Duell, wie das Telefon es sieht: Name und Symbol nur als Text, die Farbe nur als Platz der Palette.</summary>
    public sealed class ProxyDuelTeamInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Symbol { get; set; } = string.Empty;

        public int ColorIndex { get; set; } = -1;

        /// <summary>Wer für das Team spielt; <c>null</c>, solange es wählt.</summary>
        public string? ProxyName { get; set; }

        /// <summary>Ob der Stellvertreter gelost wurde.</summary>
        public bool ByLot { get; set; }

        /// <summary>Ob es das Team dieses Telefons ist.</summary>
        public bool IsOwn { get; set; }

        /// <summary>Erst am Ende: ob das Team gewonnen hat.</summary>
        public bool IsWinner { get; set; }
    }

    /// <summary>Was ein Telefon im Stellvertreter-Duell sieht: erst die Wahl, dann - nur als Stellvertreter - die Disziplin.</summary>
    public sealed class ProxyDuelClientInfo
    {
        /// <summary>Die Disziplin als Wert des Duells (<see cref="DuellEvaluator.ReaktionVariant"/> bzw. Schere-Stein-Papier).</summary>
        public int Discipline { get; set; }

        /// <summary>Dieses Telefon wählt gerade für sein Team.</summary>
        public bool ChoosesProxy { get; set; }

        /// <summary>Die Mitglieder des eigenen Teams - nur für den, der wählt.</summary>
        public List<MinigamePlayerOption> Options { get; set; } = new();

        /// <summary>Ob dieses Telefon für sein Team spielt - sonst schaut es zu.</summary>
        public bool IsProxy { get; set; }

        public List<ProxyDuelTeamInfo> Teams { get; set; } = new();

        /// <summary>Schere-Stein-Papier: der eigene Zug der laufenden bzw. zuletzt geschlossenen Runde.</summary>
        public SchereSteinPapier? OwnMove { get; set; }

        /// <summary>Der Zug des anderen Stellvertreters - erst nach dem Schließen der Runde.</summary>
        public SchereSteinPapier? OtherMove { get; set; }

        public int OwnWins { get; set; }

        public int OtherWins { get; set; }

        /// <summary>Bei der Reaktion: was <c>reaktion.js</c> braucht - nur für Stellvertreter.</summary>
        public ReactionClientInfo? Reaction { get; set; }

        /// <summary>Erst am Ende: ob das eigene Team gewonnen hat.</summary>
        public bool IsWinner { get; set; }

        /// <summary>Erst am Ende: niemand hat gewonnen.</summary>
        public bool IsDraw { get; set; }
    }

    /// <summary>
    /// Das Stellvertreter-Duell am Server (M37 T10): erst wählt jedes Team seinen Stellvertreter (<see cref="ProxyChoice"/>), dann
    /// spielen nur die Gewählten - Reaktion (<see cref="ReactionRules"/>) oder, bei genau zwei Teams, Schere-Stein-Papier Best of n.
    /// Wer mitspielt, hält der Start der ersten Runde fest; die Disziplin ebenso. Gewertet wird im Fragefenster.
    /// </summary>
    public sealed class ProxyDuelRules : IMinigameRules
    {
        private readonly MinigameRulesContext kontext;
        private readonly ProxyChoice wahl;
        private readonly ReactionRules reaktion;
        private readonly int variante;
        private readonly int runden;
        private readonly SortedDictionary<int, Dictionary<Guid, SchereSteinPapier>> geschlossen = new();
        private Dictionary<Guid, SchereSteinPapier> zuege = new();
        private int runde;

        public ProxyDuelRules(MinigameSetup setup, MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(setup);
            ArgumentNullException.ThrowIfNull(kontext);

            this.kontext = kontext;
            wahl = new ProxyChoice(kontext);
            reaktion = new ReactionRules(kontext.Zufall, kontext.NameVon);
            variante = setup.Variant;
            runden = Math.Max(1, setup.TotalRounds);
        }

        public MinigameKind Kind => MinigameKind.ProxyDuel;

        /// <summary>Die Disziplin - fest ab dem Start, aus Spielweise und Zahl der Teams.</summary>
        public int Disziplin { get; private set; } = DuellEvaluator.ReaktionVariant;

        private bool IstSchereSteinPapier => Disziplin == DuellEvaluator.SchereSteinPapierVariant;

        /// <summary>Ob noch ein Team wählt - dann kann der Spielleiter auslosen.</summary>
        public bool WahlOffen => wahl.Offen;

        /// <summary>Die Teams, die beim Start mitspielen, nach Kennung.</summary>
        public IReadOnlyList<Guid> Teams => wahl.TeamIds;

        /// <summary>Je Team sein Stellvertreter, soweit gewählt.</summary>
        public IReadOnlyList<Stellvertreter> Vertreter => wahl.Vertreter;

        /// <summary>Der Stand über die gezählten Runden; vor dem Ende der Wahl ohne Stellvertreter.</summary>
        public StellvertreterErgebnis Ergebnis()
            => IstSchereSteinPapier
                ? StellvertreterEvaluator.WerteSchereSteinPapier(wahl.Fertig ? Vertreter : [], geschlossen.Values)
                : StellvertreterEvaluator.WerteReaktion(wahl.Fertig ? Vertreter : [], reaktion.ClosedRounds);

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => !wahl.Fertig ? new Dictionary<Guid, object>()
                : IstSchereSteinPapier ? zuege.ToDictionary(z => z.Key, z => (object)z.Value)
                : reaktion.CurrentSubmissions;

        /// <summary>Während der Wahl die Mitglieder der Teams ohne Stellvertreter, danach nur die Stellvertreter.</summary>
        public bool Plays(Guid playerId) => wahl.Offen ? wahl.WaehltJetzt(playerId) : wahl.IstVertreter(playerId);

        public bool IsDecided => wahl.Fertig && StellvertreterEvaluator.IstEntschieden(Ergebnis(), runden);

        public string? WatcherText => "Die Stellvertreter spielen – du schaust zu.";

        /// <summary>Wer wählt, sieht seinen Stellvertreter; wer nicht mitspielt, warum.</summary>
        public string? WatcherTextFor(Player? player)
        {
            if (player == null || !wahl.Festgelegt)
                return WatcherText;

            if (wahl.TeamVon(player.Id) is not { } team)
                return "Beim Start warst du in keinem Team oder nicht verbunden – diesmal spielst du nicht mit.";

            if (wahl.VertreterVon(team) is not { } vertreter)
                return WatcherText;

            var name = kontext.NameVon(vertreter.SpielerId) ?? "?";
            return wahl.Offen ? $"Euer Stellvertreter: {name} – die anderen Teams wählen noch." : $"{name} spielt für euer Team – du schaust zu.";
        }

        public void StartRound(int round)
        {
            runde = round;
            zuege = new();
            geschlossen.Remove(round);
            reaktion.StartRound(round);

            if (wahl.Festgelegt)
                return;

            wahl.LegeFest();
            Disziplin = StellvertreterEvaluator.Disziplin(variante, wahl.TeamIds.Count);
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (wahl.Offen)
            {
                if (wahl.Accept(player, payload) is not { } vertreter)
                    return null;

                return wahl.Fertig ? new ProxiesComplete(vertreter, Vertreter) : new MinigameSeenByAll(new ProxyChosen(vertreter));
            }

            // Die Regel prüft selbst noch einmal - nicht nur der Rahmen über Plays.
            if (!wahl.IstVertreter(player.Id))
                return null;

            if (!IstSchereSteinPapier)
                return reaktion.Accept(player, payload);

            if (MinigamePayload.Read<DuelPayload>(payload)?.Move is not { } zug || !Enum.IsDefined(zug))
                return null;

            zuege[player.Id] = zug;
            return zug;
        }

        public bool HasSubmitted(Guid playerId)
            => wahl.Fertig && (IstSchereSteinPapier ? zuege.ContainsKey(playerId) : reaktion.HasSubmitted(playerId));

        public void CloseRound()
        {
            if (IstSchereSteinPapier)
                geschlossen[runde] = zuege;
            else
                reaktion.CloseRound();
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
            Disziplin = DuellEvaluator.ReaktionVariant;
        }

        /// <summary>Der Spielleiter lost für jedes Team ohne Stellvertreter (E1) - danach beginnt die Disziplin.</summary>
        public bool LoseOffene() => wahl.Offen && wahl.LoseOffene().Count > 0;

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var ende = phase == MinigamePhase.Finished;
            var ergebnis = wahl.Fertig ? Ergebnis() : null;
            var eigenes = player != null ? wahl.TeamVon(player.Id) : null;

            var info = new ProxyDuelClientInfo
            {
                Discipline = Disziplin,
                ChoosesProxy = player != null && phase == MinigamePhase.Running && wahl.WaehltJetzt(player.Id),
                Options = phase == MinigamePhase.Running ? wahl.OptionsFor(player) : new(),
                Teams = wahl.TeamIds.Select(t => TeamInfo(t, t == eigenes, ende && ergebnis?.IstSiegerteam(t) == true)).ToList(),
                IsWinner = ende && eigenes is { } id && ergebnis?.IstSiegerteam(id) == true,
                IsDraw = ende && ergebnis is { Sieger.Count: 0 },
            };

            if (player == null || ergebnis == null || !wahl.IstVertreter(player.Id))
                return info;

            info.IsProxy = true;

            if (IstSchereSteinPapier)
                FuelleZug(info, player.Id, phase, ergebnis);
            else
                info.Reaction = (ReactionClientInfo?)reaktion.InfoFor(player, phase);

            return info;
        }

        private ProxyDuelTeamInfo TeamInfo(Guid teamId, bool eigenes, bool sieger)
        {
            var eintrag = wahl.Team(teamId);
            var vertreter = wahl.VertreterVon(teamId);

            return new ProxyDuelTeamInfo
            {
                Name = eintrag?.Name ?? string.Empty,
                Symbol = eintrag?.Symbol ?? string.Empty,
                ColorIndex = eintrag?.ColorIndex ?? -1,
                ProxyName = vertreter is { } v ? kontext.NameVon(v.SpielerId) ?? "?" : null,
                ByLot = vertreter?.Gelost == true,
                IsOwn = eigenes,
                IsWinner = sieger,
            };
        }

        private void FuelleZug(ProxyDuelClientInfo info, Guid spieler, MinigamePhase phase, StellvertreterErgebnis ergebnis)
        {
            var anderer = Vertreter.Select(v => v.SpielerId).FirstOrDefault(id => id != spieler);
            var gezeigt = phase == MinigamePhase.Running ? zuege : geschlossen.GetValueOrDefault(runde);

            info.OwnWins = ergebnis.Duell?.SiegeVon(spieler) ?? 0;
            info.OtherWins = ergebnis.Duell?.SiegeVon(anderer) ?? 0;
            info.OwnMove = gezeigt != null && gezeigt.TryGetValue(spieler, out var eigener) ? eigener : null;

            // Geheim bis zur Auflösung: solange die Runde läuft, nie der fremde Zug.
            info.OtherMove = phase != MinigamePhase.Running && gezeigt != null && gezeigt.TryGetValue(anderer, out var fremder) ? fremder : null;
        }
    }
}
