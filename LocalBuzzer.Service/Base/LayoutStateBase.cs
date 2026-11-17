using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Hubs.Accessors;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;

namespace LocalBuzzer.Service.Base
{
    public sealed class LayoutStateManager
    {
        public List<IBuzzerLayoutState> States { get; } = new();

        public BuzzerControlsLayout CurrentLayout { get; private set; } = BuzzerControlsLayout.None;
        public int Round { get; private set; }

        /// <summary>
        /// Wie oft die Runde zurueckgesetzt wurde, seit der Server laeuft.
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> die Telefonseite baut ihr Layout nur neu auf, wenn sich
        /// die Kennzeichnung der Frage aendert. Beim Zuruecksetzen bleibt sie gleich - das
        /// Telefon entsperrte also nur, und ein Spieler, der bei Multiple Choice schon
        /// abgegeben hatte, blieb fuer immer gesperrt: sein Knopf trug weiter "Abgegeben".
        /// </para>
        /// <para>
        /// <b><see cref="Round"/> taugt dafuer nicht</b> - <c>ResetRoundAsync</c> wird mit
        /// derselben Rundennummer gerufen, die Zahl aendert sich beim Zuruecksetzen nicht. Dieser
        /// Zaehler steigt bei jedem Zuruecksetzen und macht die Runde auf der Telefonseite
        /// unterscheidbar.
        /// </para>
        /// </summary>
        public int ResetCount { get; private set; }
        public IBuzzerLayoutState? CurrentState { get; private set; }

        public bool AllLocked { get; private set; }

        /// <summary>
        /// Der Countdown ist abgelaufen (M33, 2026-10-23): keine Eingabe wird mehr angenommen, bis
        /// der Spielleiter ausdrücklich freigibt. <b>Anders als <see cref="LockAll"/></b> schließt
        /// das die Runde nicht - es wird nichts ausgewertet, und nach der Freigabe geht es weiter,
        /// wo es stand. Eine neue Runde hebt die Sperre auf.
        /// </summary>
        public bool ZeitAbgelaufen => zeitAbgelaufen;

        // Gesetzt vom Oberflächen-Thread, gelesen von den Hub-Threads.
        private volatile bool zeitAbgelaufen;

        public void SperreWegenZeit() => zeitAbgelaufen = true;

        public void GibFrei() => zeitAbgelaufen = false;

        public BuzzerState BuzzerState { get; }
        public BuzzerKeySelector BuzzerKeySelector { get; }
        public BuzzerInputState BuzzerInputState { get; }
        public MinigameState MinigameState { get; }

        /// <summary>Wer die gewoehnliche Frage beantworten darf (M35 C3b) - ueberlebt jedes Zuruecksetzen der Runde.</summary>
        public AnswerLock AnswerLock { get; } = new();

        /// <summary>Welche Teams bei dieser Frage schon gebuzzert haben (M37 T4) - ueberlebt jedes Zuruecksetzen der Runde.</summary>
        public TeamBuzzLock TeamBuzzLock { get; } = new();

        /// <summary>Die Teams (M37 T4): ein Schnappschuss vom WPF, ersetzt, nie geaendert - gelesen von den Hub-Threads.</summary>
        public TeamRoster Teams => teams;

        private volatile TeamRoster teams = TeamRoster.Empty;

        public void SetTeams(TeamRoster roster) => teams = roster ?? throw new ArgumentNullException(nameof(roster));

        /// <summary>Die Team-Abstimmung (M37 T7): jedes Telefon sieht nur die eigene Stimme und sein eigenes Team.</summary>
        public TeamVoteBoard TeamVote { get; } = new();

        private readonly GameAccessor _gameAccessor;

        public LayoutStateManager(GameAccessor gameAccessor)
        {
            _gameAccessor = gameAccessor;

            BuzzerState = new(gameAccessor, AnswerLock, TeamBuzzLock, () => teams);
            BuzzerKeySelector = new(gameAccessor, AnswerLock);
            BuzzerInputState = new(gameAccessor, AnswerLock);
            MinigameState = new(gameAccessor, () => teams);

            States.Add(BuzzerState);
            States.Add(BuzzerKeySelector);
            States.Add(BuzzerInputState);
            States.Add(MinigameState);
        }

        public void ResetLayouts(int round, BuzzerControlsLayout layout = BuzzerControlsLayout.None)
        {
            CurrentLayout = layout;
            Round = round;
            AllLocked = false;
            zeitAbgelaufen = false;

            ResetCount++;

            // M37 T7: eine neue Runde stimmt neu ab - eine Antwort aus der alten gilt nicht mehr.
            TeamVote.Reopen();

            CurrentState = States.SingleOrDefault(s => s.BuzzerControlsLayout == layout);

            foreach (var state in States)
            {
                if (state.BuzzerControlsLayout == CurrentLayout)
                    state.Reset();
                else
                    state.ClearAndLock();
            }
        }

        public void LockAll()
        {
            CurrentState?.LockAll();

            foreach (var state in States)
            {
                if (state == CurrentState) continue;
                state.LockAll();
            }

            AllLocked = true;
        }

        public ClientLayoutStateDto CreateClientState(Player? player = null)
        {
            // M35 C3b: wer die Frage nicht beantworten darf, ist gesperrt - die Vorphase im Minispiel-Rahmen nicht.
            var ausgeschlossen = player != null && CurrentLayout != BuzzerControlsLayout.Minigame && !AnswerLock.Allows(player.Id);

            // M37 T4: hat das Team schon gebuzzert, sagt es das Telefon, sobald die Runde wieder offen ist - bis dahin der Gewinner.
            var teamGesperrt = player != null && !ausgeschlossen && CurrentLayout == BuzzerControlsLayout.Buzzer
                && BuzzerState.Winner == null && TeamBuzzLock.Blocks(teams, player.Id);

            return new ClientLayoutStateDto
            {
                PlayerName = player?.CalculatedDisplayName,
                Round = Round,
                ResetCount = ResetCount,
                Layout = CurrentLayout,
                CurrentLayoutLocked = (CurrentState?.IsLockedFor(player) ?? true) || ZeitAbgelaufen || ausgeschlossen || teamGesperrt,
                ZeitAbgelaufen = ZeitAbgelaufen,
                ExcludedFromQuestion = ausgeschlossen || teamGesperrt,
                ExcludedReason = ausgeschlossen && player != null ? AnswerLock.ReasonFor(player.Id) : teamGesperrt ? TeamBuzzLock.Satz : null,
                AllLocked = AllLocked,
                LayoutInfo = CurrentState?.InfoFor(player),
                Winner = BuzzerState.Winner?.CalculatedDisplayName,
                Team = TeamFor(player)
            };
        }

        /// <summary>Das Team des Telefons, bei der Tastenwahl samt Team-Abstimmung (M37 T7) - nur seine eigene.</summary>
        private TeamClientInfo? TeamFor(Player? player)
        {
            if (player == null || teams.TeamOf(player.Id)?.ToClientInfo() is not { } team)
                return null;

            if (CurrentLayout == BuzzerControlsLayout.KeySelect)
            {
                var spieler = _gameAccessor.GetGame?.Invoke()?.Players.Where(p => p != null).Select(p => p.Id) ?? [];
                team.Vote = TeamVote.InfoFor(player.Id, teams, spieler, BuzzerKeySelector, AnswerLock, AllLocked || BuzzerKeySelector.Locked);
            }

            return team;
        }
    }
}