namespace Quizzer.Views.BuzzerViews.Firewall
{
    /// <summary>Wie die Firewall zum Buzzer-Server steht.</summary>
    public enum FirewallState
    {
        /// <summary>Der Port ist in allen aktiven Netzen freigegeben.</summary>
        Freigegeben,

        /// <summary>Es gibt eine Regel, aber sie gilt nicht fuer jedes aktive Netz.</summary>
        FalschesProfil,

        /// <summary>Fuer dieses Programm und diesen Port gibt es gar keine Regel.</summary>
        KeineRegel,

        /// <summary>Die Firewall liess sich nicht befragen.</summary>
        Unbekannt
    }

    /// <summary>
    /// Das Ergebnis der Firewall-Pruefung samt fertigem Satz fuer den Spielleiter.
    /// </summary>
    /// <param name="State">Die Lage.</param>
    /// <param name="MissingProfiles">Aktive Netzprofile ohne Freigabe, deutsch benannt.</param>
    /// <param name="Hint">Was der Spielleiter lesen soll; leer, wenn alles freigegeben ist.</param>
    public sealed record FirewallVerdict(
        FirewallState State,
        IReadOnlyList<string> MissingProfiles,
        string Hint)
    {
        /// <summary>Ob ein Hinweis angezeigt werden soll.</summary>
        public bool NeedsAttention => State is FirewallState.FalschesProfil or FirewallState.KeineRegel;

        public static FirewallVerdict Ok() =>
            new(FirewallState.Freigegeben, Array.Empty<string>(), string.Empty);

        public static FirewallVerdict Unknown(string hint) =>
            new(FirewallState.Unbekannt, Array.Empty<string>(), hint);
    }
}
