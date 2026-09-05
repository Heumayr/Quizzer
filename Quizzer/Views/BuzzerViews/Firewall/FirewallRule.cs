namespace Quizzer.Views.BuzzerViews.Firewall
{
    /// <summary>
    /// Eine eingehende Regel der Windows-Firewall, so weit sie fuer den Buzzer-Server zaehlt.
    /// Absichtlich ein eigener Typ statt der COM-Schnittstelle: so laesst sich die Bewertung
    /// ohne Firewall pruefen.
    /// </summary>
    /// <param name="Name">Anzeigename der Regel.</param>
    /// <param name="ApplicationName">Pfad des Programms, oder leer bei einer reinen Portregel.</param>
    /// <param name="Enabled">Ob die Regel aktiv ist.</param>
    /// <param name="Inbound">Ob sie eingehenden Verkehr betrifft.</param>
    /// <param name="Allow">Ob sie erlaubt statt blockiert.</param>
    /// <param name="Profiles">Bitmaske der Netzwerkprofile (1 Domaene, 2 Privat, 4 Oeffentlich).</param>
    /// <param name="Protocol">6 fuer TCP, 17 fuer UDP, 256 fuer beliebig.</param>
    /// <param name="LocalPorts">Portangabe der Regel, etwa <c>*</c>, <c>5000</c> oder <c>4000-6000</c>.</param>
    public sealed record FirewallRule(
        string Name,
        string? ApplicationName,
        bool Enabled,
        bool Inbound,
        bool Allow,
        int Profiles,
        int Protocol,
        string LocalPorts);
}
