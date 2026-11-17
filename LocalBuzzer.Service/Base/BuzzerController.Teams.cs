namespace LocalBuzzer.Service.Base
{
    /// <summary>Teams am Hub (M37 T4): die Aufstellung vom WPF und die Freigabe der Team-Sperre durch die Spielleitung.</summary>
    public partial class BuzzerController
    {
        /// <summary>
        /// Ersetzt die Aufstellung und schickt jedem Telefon sein Team - beim Öffnen des Spiels, beim Serverstart und nach
        /// dem Umsetzen. Eine schon gesetzte Team-Sperre bleibt: sie hängt an der Team-Kennung, nicht an der Aufstellung.
        /// </summary>
        public async Task SetTeamsAsync(TeamRoster roster, CancellationToken ct = default)
        {
            CheckServerRunning();

            StateManager.SetTeams(roster);

            await SendStateAsync(ct);
        }

        /// <summary>„Team wieder freigeben“ (E1): wer bei dieser Frage schon gebuzzert hat, darf wieder.</summary>
        public async Task ReleaseTeamsAsync(CancellationToken ct = default)
        {
            CheckServerRunning();

            StateManager.TeamBuzzLock.Clear();

            await SendStateAsync(ct);
        }

        /// <summary>Die Team-Abstimmung beginnt (M37 T7) - die Telefone erfahren es mit dem Ausspielen der Runde danach.</summary>
        public void StartTeamVote()
        {
            CheckServerRunning();

            StateManager.TeamVote.Start();
        }

        /// <summary>
        /// Die Antworten der Teams nach dem Schluss, je Team so, wie sein Telefon sie zeigt (M37 T7) - jedes Telefon bekommt nur die
        /// seines Teams. Erneut nach einem Entscheid der Spielleitung.
        /// </summary>
        public async Task SetTeamVoteResultsAsync(IReadOnlyDictionary<Guid, string> ergebnisse, CancellationToken ct = default)
        {
            CheckServerRunning();

            StateManager.TeamVote.SetResults(ergebnisse);

            await SendStateAsync(ct);
        }

        /// <summary>Die Frage ist zu: keine Abstimmung mehr - das Zurücksetzen danach sagt es den Telefonen.</summary>
        public void EndTeamVote() => StateManager?.TeamVote.End();
    }
}
