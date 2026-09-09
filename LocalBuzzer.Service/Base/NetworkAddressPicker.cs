using System.Net;

namespace LocalBuzzer.Service.Base
{
    /// <summary>Was der Server über einen Netzwerkadapter wissen muss, um zu wählen.</summary>
    /// <param name="Description">Der Klartextname des Adapters - daran werden virtuelle erkannt.</param>
    /// <param name="IsUp">Ob er betriebsbereit ist.</param>
    /// <param name="IsLoopback">Ob es der Rückschleifen-Adapter ist.</param>
    /// <param name="IsTunnel">Ob es ein Tunnel ist (VPN und Verwandte).</param>
    /// <param name="HasIPv4Gateway">Ob ein IPv4-Gateway hinterlegt ist - das trennt das echte LAN vom Rest.</param>
    /// <param name="IPv4Addresses">Seine IPv4-Adressen.</param>
    public sealed record NetworkAdapterInfo(
        string Description,
        bool IsUp,
        bool IsLoopback,
        bool IsTunnel,
        bool HasIPv4Gateway,
        IReadOnlyList<string> IPv4Addresses);

    /// <summary>
    /// Wählt die Adresse, die auf dem QR-Code landet - die eine Zahl, an der der ganze Abend
    /// hängt.
    /// <para>
    /// <b>Herausgezogen am 2026-09-09, weil sie nichts gehalten hat.</b> Die Regel stand als
    /// LINQ-Abfrage mitten in <c>BuzzerServer.GetLocalIPs</c> über
    /// <c>NetworkInterface.GetAllNetworkInterfaces()</c> - also über den Zustand dieses
    /// Rechners, und damit über nichts, was sich zusichern lässt. Am selben Tag fiel auf, dass
    /// die im Gedächtnis notierte Adresse längst in einem anderen Subnetz lag: <i>die Auswahl
    /// hatte den Wechsel mitgemacht, und niemand konnte sagen, ob zu Recht.</i>
    /// </para>
    /// <para>
    /// <b>Das IPv4-Gateway ist das entscheidende Merkmal</b>, nicht der Name: ein Adapter ohne
    /// Gateway hängt an keinem Netz, über das ein Telefon kommt. Die Namensfilter fangen nur die
    /// virtuellen Adapter ab, die trotzdem eines melden.
    /// </para>
    /// </summary>
    public static class NetworkAddressPicker
    {
        /// <summary>Namensbestandteile, an denen ein virtueller Adapter erkannt wird.</summary>
        private static readonly string[] Virtuell = ["Virtual", "Hyper-V", "VMware", "TAP"];

        /// <summary>
        /// Die brauchbaren Adressen, beste zuerst: <c>192.168.*</c> vor <c>10.*</c> vor allem
        /// anderen, innerhalb einer Gruppe alphabetisch.
        /// </summary>
        /// <param name="adapter">Alle Adapter des Rechners.</param>
        /// <param name="includeLoopback">Ob <c>127.*</c> mitgenommen wird.</param>
        public static string[] Pick(IEnumerable<NetworkAdapterInfo> adapter, bool includeLoopback)
        {
            ArgumentNullException.ThrowIfNull(adapter);

            var kandidaten =
                from a in adapter
                where a.IsUp
                where !a.IsLoopback
                where !a.IsTunnel
                where !Virtuell.Any(v => a.Description.Contains(v, StringComparison.OrdinalIgnoreCase))

                // Das echte LAN: ohne Gateway kommt kein Telefon durch.
                where a.HasIPv4Gateway
                from ip in a.IPv4Addresses
                where includeLoopback || !IstRueckschleife(ip)
                select ip;

            return kandidaten
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(ip => ip.StartsWith("192.168.", StringComparison.Ordinal))
                .ThenByDescending(ip => ip.StartsWith("10.", StringComparison.Ordinal))
                .ThenBy(ip => ip, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IstRueckschleife(string ip)
            => IPAddress.TryParse(ip, out var adresse) && IPAddress.IsLoopback(adresse);
    }
}
