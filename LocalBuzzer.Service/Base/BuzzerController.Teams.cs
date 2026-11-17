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
    }
}
