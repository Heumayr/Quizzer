using Quizzer.DataModels.Models.Base;

namespace LocalBuzzer.Service.Base
{
    /// <summary>Ein Probetelefon: der Spieler und die Adresse, unter der sein Browserfenster spielt.</summary>
    public sealed record ProbePhoneLink(Player Player, Uri Url);

    /// <summary>
    /// Probetelefone am PC (M35 B3, V1): je Mitspieler eine Adresse mit seinem abgeleiteten Schluessel,
    /// zum Vorbereiten ohne Handys. <b>Nie die rohe Id</b> - sie ist seit M10 kein Geheimnis und darf
    /// nirgends als Ausweis dienen (<see cref="BuzzerAccessKeys"/>).
    /// </summary>
    public static class ProbePhoneLinks
    {
        /// <param name="players">Die Mitspieler des Spiels.</param>
        /// <param name="keys">Die Schluessel dieses Rechners.</param>
        /// <param name="hostAndPort">Etwa <c>localhost:5000</c> - das Fenster laeuft auf demselben Rechner.</param>
        public static IReadOnlyList<ProbePhoneLink> For(IEnumerable<Player> players, BuzzerAccessKeys keys, string hostAndPort)
        {
            ArgumentNullException.ThrowIfNull(players);
            ArgumentNullException.ThrowIfNull(keys);

            return players
                .Select(p => new ProbePhoneLink(p,
                    new Uri($"http://{hostAndPort}/?key={Uri.EscapeDataString(keys.SchluesselFuer(p.Id))}")))
                .ToList();
        }
    }
}
