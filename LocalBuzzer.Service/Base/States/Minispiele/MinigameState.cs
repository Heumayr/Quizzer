using LocalBuzzer.Service.Hubs.Accessors;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Wie der Rahmen eine Abgabe aufgenommen hat.</summary>
    /// <param name="Accepted">Ob sie zaehlt.</param>
    /// <param name="Value">Was die Art daraus gemacht hat.</param>
    /// <param name="Closed">Gesetzt, wenn die Abgabe die Runde geschlossen hat.</param>
    /// <param name="Restarted">Die Abgabe hat eine neue Stufe geoeffnet (Spielerwahl, M35 C2) - alle bekommen ihren Zustand.</param>
    /// <param name="ForAll">Die Abgabe aendert, was alle sehen (M35 C5) - alle bekommen ihren Zustand, im selben Durchgang.</param>
    public sealed record MinigameSubmitResult(
        bool Accepted, object? Value = null, MinigameRoundClosed? Closed = null, bool Restarted = false, bool ForAll = false)
    {
        public static readonly MinigameSubmitResult Rejected = new(false);
    }

    /// <summary>
    /// Der Rahmen der Minispiele am Server (M35 B3, <see cref="BuzzerControlsLayout.Minigame"/>): Phase,
    /// Runden, Durchgang, Sperre je Spieler und der Versand je Spieler. Was eine Art ausmacht, steht in
    /// ihren <see cref="IMinigameRules"/>.
    /// <para>
    /// <b>Jede Abgabe kommt aus dem WLAN</b> und wird hier geprueft, bevor die Art sie sieht: nur in
    /// einer laufenden Runde, nur fuer die laufende Art und den laufenden Durchgang, nicht von einem
    /// gesperrten Spieler und nur einmal je Runde. Wer abgibt, sagt die Verbindung (Hub).
    /// </para>
    /// </summary>
    public sealed class MinigameState : IBuzzerLayoutState
    {
        private readonly object gate = new();
        private readonly GameAccessor gameAccessor;
        private readonly HashSet<Guid> gesperrt = new();
        private MinigameSetup setup = new(default, string.Empty, string.Empty, 1);
        private IMinigameRules? rules;

        // Zufaelliger Anfang: ein Telefon aus einem frueheren Serverlauf darf nicht zufaellig treffen.
        private int durchgang = Random.Shared.Next(1, 1 << 20) * 1000;

        private readonly Func<TeamRoster> teams;

        public MinigameState(GameAccessor gameAccessor, Func<TeamRoster>? teams = null)
        {
            this.gameAccessor = gameAccessor;
            this.teams = teams ?? (() => TeamRoster.Empty);
        }

        /// <summary>Zufallsquelle fuer die Geheimnisse der Arten, wie <see cref="Random.Next(int, int)"/>.</summary>
        public Func<int, int, int> Zufall { get; set; } = Random.Shared.Next;

        /// <summary>Die Serveruhr (M35 C4): sie entscheidet, ob ein Ausstieg vor dem Absturz kam - eigene nur fuer Tests.</summary>
        public TimeProvider Uhr { get; set; } = TimeProvider.System;

        public BuzzerControlsLayout BuzzerControlsLayout => BuzzerControlsLayout.Minigame;

        /// <summary>Gesperrt heisst: es laeuft keine Runde, nichts wird angenommen.</summary>
        public bool Locked { get; set; } = true;

        public MinigamePhase Phase { get; private set; }

        public int Round { get; private set; }

        public bool Aborted { get; private set; }

        public MinigameSetup Setup => setup;

        /// <summary>Die Regeln der eingestellten Art; <c>null</c> bei einer unbekannten.</summary>
        public IMinigameRules? Rules => rules;

        public int Durchgang
        {
            get { lock (gate) return durchgang; }
        }

        /// <summary>Stellt Art und Runden ein - vor dem Ausspielen der Frage.</summary>
        public void Configure(MinigameSetup neu)
        {
            ArgumentNullException.ThrowIfNull(neu);

            lock (gate)
            {
                setup = neu with { TotalRounds = Math.Max(1, neu.TotalRounds) };
                rules = MinigameRulesFactory.For(setup,
                    new((a, b) => Zufall(a, b), NameVon, Mitspieler, () => Uhr.GetElapsedTime(0), id => teams().TeamOf(id)?.TeamId,
                        () => teams().IsTeamGame, id => teams().TeamOf(id)));
                ClearLocked();
            }
        }

        public object BuzzerStateInfo => InfoFor(null);

        public object InfoFor(Player? player)
        {
            lock (gate)
            {
                return new MinigameClientInfo
                {
                    Kind = setup.Kind,
                    SpecialRule = setup.SpecialRule,
                    PrePhase = setup.PrePhase,
                    Title = setup.Title,
                    RulesText = setup.RulesText,
                    Phase = Phase,
                    CurrentRound = Round,
                    TotalRounds = setup.TotalRounds,
                    Durchgang = durchgang,
                    CountdownSeconds = setup.CountdownSeconds,
                    Aborted = Aborted,
                    OwnSubmitted = player != null && rules?.HasSubmitted(player.Id) == true,
                    KindInfo = rules?.InfoFor(player, Phase),
                    WatcherText = rules?.WatcherTextFor(player),
                };
            }
        }

        public bool IsLockedFor(Player? player)
        {
            lock (gate)
            {
                return player == null
                    || Locked
                    || Phase != MinigamePhase.Running
                    || gesperrt.Contains(player.Id)
                    || rules?.Plays(player.Id) == false
                    || rules?.HasSubmitted(player.Id) == true;
            }
        }

        // --- Sperre je Spieler: Duell, Auktion, Experte und Allianz lassen nur einige spielen ---

        public void LockPlayer(Guid playerId)
        {
            lock (gate) gesperrt.Add(playerId);
        }

        public void UnlockPlayer(Guid playerId)
        {
            lock (gate) gesperrt.Remove(playerId);
        }

        public bool IsPlayerLocked(Guid playerId)
        {
            lock (gate) return gesperrt.Contains(playerId);
        }

        /// <summary>
        /// Der Spielleiter legt fest, wer spielt (E1, M35 C2) - wenn der Ausloeser fehlt oder er ueberschreibt.
        /// Mitten in der Runde gilt ein neuer Durchgang, eine Abgabe davor zaehlt nicht mehr.
        /// </summary>
        public bool SetPair(Guid chooserId, Guid? partnerId)
        {
            lock (gate)
            {
                if (rules is not IMinigamePairRules paar || !paar.SetPair(chooserId, partnerId))
                    return false;

                if (Phase == MinigamePhase.Running)
                    durchgang++;

                return true;
            }
        }

        // --- Der Spielleiter ---

        /// <summary>Ein Zugriff des Spielleiters auf die Regeln der Art, unter der Sperre des Rahmens (Bluff: streichen, M35 C6).</summary>
        public T Lenke<T>(Func<IMinigameRules?, T> zugriff)
        {
            ArgumentNullException.ThrowIfNull(zugriff);

            lock (gate) return zugriff(rules);
        }

        /// <summary>
        /// Ein Eingriff des Spielleiters, der die nächste Stufe der laufenden Runde öffnet (M37 T10: offene Stellvertreter auslosen) -
        /// wie eine Wahl am Telefon gilt danach ein neuer Durchgang. <c>false</c>, wenn keine Runde läuft oder der Eingriff nichts tat.
        /// </summary>
        public bool OpenNextStage(Func<IMinigameRules?, bool> eingriff)
        {
            ArgumentNullException.ThrowIfNull(eingriff);

            lock (gate)
            {
                if (Phase != MinigamePhase.Running || !eingriff(rules))
                    return false;

                durchgang++;
                return true;
            }
        }

        /// <summary>Startet die naechste Runde; <c>false</c>, wenn keine mehr aussteht oder eine laeuft.</summary>
        public bool StartNextRound()
        {
            lock (gate)
            {
                if (rules == null || Phase is MinigamePhase.Running or MinigamePhase.Finished)
                    return false;

                Round++;
                StartRun();
                return true;
            }
        }

        /// <summary>Spielt die laufende oder zuletzt gespielte Runde neu; ihre Abgaben zaehlen nicht mehr.</summary>
        public bool RepeatRound()
        {
            lock (gate)
            {
                if (rules == null || Round < 1 || Aborted)
                    return false;

                StartRun();
                return true;
            }
        }

        /// <summary>Schliesst die laufende Runde, auch wenn noch Abgaben fehlen (getrenntes Telefon).</summary>
        public MinigameRoundClosed? CloseRound()
        {
            lock (gate)
            {
                if (rules == null || Phase != MinigamePhase.Running)
                    return null;

                var abgaben = rules.CurrentSubmissions;
                rules.CloseRound();

                // Best of n (Duell, M35 C2): entschieden ist auch vor der letzten Runde zu Ende.
                var letzte = Round >= setup.TotalRounds || rules.IsDecided;
                Phase = letzte ? MinigamePhase.Finished : MinigamePhase.RoundOver;
                Locked = true;

                return new(Round, setup.TotalRounds, letzte, false, abgaben);
            }
        }

        /// <summary>Bricht ab (E1): die laufende Runde zaehlt nicht, gespielte bleiben.</summary>
        public MinigameRoundClosed? Abort()
        {
            lock (gate)
            {
                if (rules == null || Aborted)
                    return null;

                rules.DiscardRunning();
                Phase = MinigamePhase.Finished;
                Aborted = true;
                Locked = true;

                return new(Round, setup.TotalRounds, true, true, new Dictionary<Guid, object>());
            }
        }

        // --- Das Telefon ---

        /// <summary>Prueft eine Abgabe und schliesst die Runde, sobald alle verbundenen Spieler abgegeben haben.</summary>
        public MinigameSubmitResult Submit(Player player, MinigameAction action)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(action);

            lock (gate)
            {
                if (rules == null || Locked || Phase != MinigamePhase.Running)
                    return MinigameSubmitResult.Rejected;

                // Fremde Art, Sonderregel oder Vorphase, alter Durchgang, gesperrter Spieler, Zuschauer, zweite Abgabe: verworfen.
                if (action.Kind != setup.Kind || action.SpecialRule != setup.SpecialRule || action.PrePhase != setup.PrePhase
                    || action.Durchgang != durchgang
                    || gesperrt.Contains(player.Id) || !rules.Plays(player.Id) || rules.HasSubmitted(player.Id))
                    return MinigameSubmitResult.Rejected;

                var wert = rules.Accept(player, action.Payload);

                if (wert == null)
                    return MinigameSubmitResult.Rejected;

                // Eine Spielerwahl oeffnet die naechste Stufe: neuer Durchgang, eine Abgabe davor zaehlt nicht mehr.
                if (wert is IMinigameStageChange)
                {
                    durchgang++;
                    return new(true, wert, Restarted: true);
                }

                var fuerAlle = wert is MinigameSeenByAll;
                if (wert is MinigameSeenByAll alle)
                    wert = alle.Value;

                return new(true, wert, AlleHabenAbgegeben() ? CloseRound() : null, ForAll: fuerAlle);
            }
        }

        /// <summary>
        /// Nach einer Trennung: fehlt nur noch, wer gerade gegangen ist, ist die Runde zu. Ein getrenntes
        /// Telefon gibt nichts ab - der Spielleiter wertet es von Hand (E1).
        /// </summary>
        public MinigameRoundClosed? CloseIfComplete()
        {
            lock (gate)
            {
                return Phase == MinigamePhase.Running && AlleHabenAbgegeben() ? CloseRound() : null;
            }
        }

        /// <summary>Ob die eingestellte Art eine Frist an der Serveruhr hat (M35 C4) - dann wacht der Controller.</summary>
        public bool HasDeadline
        {
            get { lock (gate) return rules is IMinigameDeadlineRules; }
        }

        /// <summary>Schliesst die laufende Runde, wenn ihre Frist an der Serveruhr abgelaufen ist (Zocker: Absturz).</summary>
        public MinigameRoundClosed? CloseIfDue()
        {
            lock (gate)
            {
                return Phase == MinigamePhase.Running && rules is IMinigameDeadlineRules { IsDue: true } ? CloseRound() : null;
            }
        }

        // --- IBuzzerLayoutState ---

        public void Reset()
        {
            lock (gate)
            {
                ClearLocked();
                Locked = true;
            }
        }

        public void ClearAndLock()
        {
            lock (gate)
            {
                ClearLocked();
                Locked = true;
            }
        }

        public void LockAll()
        {
            lock (gate)
            {
                Locked = true;
            }
        }

        private void StartRun()
        {
            durchgang++;
            rules!.StartRound(Round);
            Phase = MinigamePhase.Running;
            Locked = false;
        }

        private void ClearLocked()
        {
            rules?.Clear();
            gesperrt.Clear();
            Phase = MinigamePhase.Waiting;
            Round = 0;
            Aborted = false;
        }

        /// <summary>Alle verbundenen, nicht gesperrten Mitspieler haben abgegeben.</summary>
        private bool AlleHabenAbgegeben()
        {
            var offen = (gameAccessor.GetGame?.Invoke()?.Players ?? [])
                .Where(p => p.ConnectionState == PlayerConnection.Connected && !gesperrt.Contains(p.Id) && rules!.Plays(p.Id))
                .ToList();

            return offen.Count > 0 && offen.All(p => rules!.HasSubmitted(p.Id));
        }

        private string? NameVon(Guid id)
            => gameAccessor.GetGame?.Invoke()?.Players?.FirstOrDefault(p => p.Id == id)?.CalculatedDisplayName;

        /// <summary>Die Mitspieler des laufenden Spiels - nur sie darf eine Spielerwahl treffen (E4).</summary>
        private IReadOnlyList<Player> Mitspieler()
            => gameAccessor.GetGame?.Invoke()?.Players?.ToList() ?? [];
    }
}
