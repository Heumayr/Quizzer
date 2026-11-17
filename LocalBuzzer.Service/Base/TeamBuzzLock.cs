namespace LocalBuzzer.Service.Base
{
    /// <summary>
    /// Welche Teams bei dieser Frage schon gebuzzert haben (M37 T4, F-M37-09): nach einer falschen Antwort darf nur ein
    /// anderes Team. Gesetzt und geprüft in <c>BuzzerState.TryBuzz</c> unter dessen Sperre; überlebt Zurücksetzen und
    /// Wiederverbinden und wird erst beim Schließen der Frage geleert - oder wenn die Spielleitung freigibt (E1).
    /// <para>Die Schlüssel sind Team-Kennungen, ohne Team die des Spielers (<see cref="TeamRoster.KeyFor"/>).</para>
    /// </summary>
    public sealed class TeamBuzzLock
    {
        /// <summary>Was das Telefon eines gesperrten Teams sagt.</summary>
        public const string Satz = "Dein Team hat schon gebuzzert.";

        private readonly object gate = new();
        private readonly HashSet<Guid> gesperrt = new();

        /// <summary>Die gesperrten Schlüssel, als Kopie.</summary>
        public IReadOnlyCollection<Guid> LockedTeams
        {
            get { lock (gate) return gesperrt.ToList(); }
        }

        public bool IsLocked(Guid key)
        {
            lock (gate) return gesperrt.Contains(key);
        }

        internal void Lock(Guid key)
        {
            lock (gate) gesperrt.Add(key);
        }

        /// <summary>Ob der Spieler nach dieser Aufstellung nicht buzzern darf - ohne Teamspiel nie.</summary>
        public bool Blocks(TeamRoster roster, Guid playerId) => roster.KeyFor(playerId) is { } key && IsLocked(key);

        /// <summary>Frage zu oder „Team wieder freigeben“: alle Teams dürfen wieder.</summary>
        public void Clear()
        {
            lock (gate) gesperrt.Clear();
        }
    }
}
