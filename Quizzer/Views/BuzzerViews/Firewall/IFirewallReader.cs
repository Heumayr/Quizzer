namespace Quizzer.Views.BuzzerViews.Firewall
{
    /// <summary>
    /// Liest die Lage der Windows-Firewall. Eigene Schnittstelle, damit die Bewertung ohne
    /// Firewall und ohne Verwalterrechte pruefbar bleibt.
    /// </summary>
    public interface IFirewallReader
    {
        /// <summary>Bitmaske der aktiven Netzprofile (1 Domaene, 2 Privat, 4 Oeffentlich).</summary>
        int ActiveProfiles { get; }

        /// <summary>Alle Regeln der Firewall. Leer, wenn sie sich nicht befragen liess.</summary>
        IReadOnlyList<FirewallRule> ReadRules();

        /// <summary>Ob die letzte Abfrage gelungen ist.</summary>
        bool IsAvailable { get; }
    }
}
