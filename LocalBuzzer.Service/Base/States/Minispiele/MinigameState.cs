using LocalBuzzer.Service.Hubs.Accessors;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Wie der Rahmen eine Abgabe aufgenommen hat.</summary>
    /// <param name="Accepted">Ob sie zaehlt.</param>
    /// <param name="Value">Was die Art daraus gemacht hat.</param>
    /// <param name="Closed">Gesetzt, wenn die Abgabe die Runde geschlossen hat.</param>
    public sealed record MinigameSubmitResult(bool Accepted, object? Value = null, MinigameRoundClosed? Closed = null)
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

        public MinigameState(GameAccessor gameAccessor)
        {
            this.gameAccessor = gameAccessor;
        }

        /// <summary>Zufallsquelle fuer die Geheimnisse der Arten, wie <see cref="Random.Next(int, int)"/>.</summary>
        public Func<int, int, int> Zufall { get; set; } = Random.Shared.Next;

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
                rules = neu.Kind switch
                {
                    MinigameKind.Reaction => new ReactionRules((a, b) => Zufall(a, b), NameVon),
                    _ => null,
                };
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

        // --- Der Spielleiter ---

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

                var letzte = Round >= setup.TotalRounds;
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

                // Fremde Art, alter Durchgang, gesperrter Spieler, zweite Abgabe: verworfen.
                if (action.Kind != setup.Kind || action.Durchgang != durchgang
                    || gesperrt.Contains(player.Id) || rules.HasSubmitted(player.Id))
                    return MinigameSubmitResult.Rejected;

                var wert = rules.Accept(player, action.Payload);

                if (wert == null)
                    return MinigameSubmitResult.Rejected;

                return new(true, wert, AlleHabenAbgegeben() ? CloseRound() : null);
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
                .Where(p => p.ConnectionState == PlayerConnection.Connected && !gesperrt.Contains(p.Id))
                .ToList();

            return offen.Count > 0 && offen.All(p => rules!.HasSubmitted(p.Id));
        }

        private string? NameVon(Guid id)
            => gameAccessor.GetGame?.Invoke()?.Players?.FirstOrDefault(p => p.Id == id)?.CalculatedDisplayName;
    }
}
