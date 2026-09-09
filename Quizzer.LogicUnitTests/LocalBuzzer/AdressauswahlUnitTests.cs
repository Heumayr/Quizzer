using LocalBuzzer.Service.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// <b>Die Adresse, die auf dem QR-Code landet.</b>
    /// <para>
    /// <b>Warum es diese Klasse erst seit 2026-09-09 gibt:</b> die Regel stand als LINQ-Abfrage
    /// mitten in <c>BuzzerServer.GetLocalIPs</c>, direkt über
    /// <c>NetworkInterface.GetAllNetworkInterfaces()</c> - also über den Zustand dieses Rechners.
    /// Sie war damit von keiner Zusicherung erreichbar. Aufgefallen ist es, als die im Gedächtnis
    /// notierte Adresse `192.168.0.92` sich als längst überholt herausstellte (der Rechner steht
    /// heute auf `192.168.68.57`): <i>die Auswahl hatte den Subnetzwechsel mitgemacht, und
    /// niemand konnte sagen, ob zu Recht.</i>
    /// </para>
    /// <para>
    /// Gemessen wird deshalb an <b>erfundenen</b> Adaptern - nur so hängt das Ergebnis nicht am
    /// Netz, in dem der Testlauf gerade steckt.
    /// </para>
    /// </summary>
    [TestClass]
    public class AdressauswahlUnitTests
    {
        private static NetworkAdapterInfo Adapter(
            string beschreibung,
            string ip,
            bool up = true,
            bool loopback = false,
            bool tunnel = false,
            bool gateway = true)
            => new(beschreibung, up, loopback, tunnel, gateway, [ip]);

        /// <summary>Der Normalfall: ein echter LAN-Adapter neben lauter Ablenkung.</summary>
        [TestMethod]
        public void TheRealLanAdapterWins()
        {
            var gewaehlt = NetworkAddressPicker.Pick(
            [
                Adapter("Hyper-V Virtual Ethernet Adapter", "172.20.1.1"),
                Adapter("VMware Network Adapter VMnet8", "192.168.200.1"),
                Adapter("TAP-Windows Adapter V9", "10.8.0.2"),
                Adapter("Marvell AQtion 10Gbit Network Adapter", "192.168.68.57"),
            ], includeLoopback: false);

            CollectionAssert.AreEqual(new[] { "192.168.68.57" }, gewaehlt,
                "Gewaehlt wurde: " + string.Join(", ", gewaehlt));
        }

        /// <summary>
        /// <b>Das Gateway ist das entscheidende Merkmal, nicht der Name.</b> Ein Adapter ohne
        /// IPv4-Gateway hängt an keinem Netz, über das ein Telefon kommt - auch wenn er
        /// unverdächtig heißt und eine hübsche Adresse trägt.
        /// </summary>
        [TestMethod]
        public void AnAdapterWithoutGatewayIsNotOffered()
        {
            var gewaehlt = NetworkAddressPicker.Pick(
            [
                Adapter("Intel Ethernet Connection", "192.168.10.5", gateway: false),
                Adapter("Realtek PCIe GbE Family Controller", "192.168.68.57"),
            ], includeLoopback: false);

            CollectionAssert.DoesNotContain(gewaehlt, "192.168.10.5",
                "Ein Adapter ohne Gateway darf nicht auf den QR-Code. Gewaehlt: "
                + string.Join(", ", gewaehlt));

            CollectionAssert.Contains(gewaehlt, "192.168.68.57");
        }

        /// <summary>Abgeschaltete, Rückschleife und Tunnel fallen heraus.</summary>
        [TestMethod]
        public void DownLoopbackAndTunnelAreSkipped()
        {
            var gewaehlt = NetworkAddressPicker.Pick(
            [
                Adapter("Ethernet (gezogen)", "192.168.1.10", up: false),
                Adapter("Software Loopback Interface 1", "127.0.0.1", loopback: true),
                Adapter("Firmen-VPN", "10.99.0.5", tunnel: true),
                Adapter("Ethernet 2", "192.168.68.57"),
            ], includeLoopback: false);

            CollectionAssert.AreEqual(new[] { "192.168.68.57" }, gewaehlt,
                "Gewaehlt wurde: " + string.Join(", ", gewaehlt));
        }

        /// <summary>
        /// Die Reihenfolge: <c>192.168.*</c> vor <c>10.*</c> vor allem anderen. Der erste Eintrag
        /// ist der, den das Buzzer-Fenster anbietet.
        /// </summary>
        [TestMethod]
        public void HomeNetworksComeFirst()
        {
            var gewaehlt = NetworkAddressPicker.Pick(
            [
                Adapter("Adapter C", "172.16.4.4"),
                Adapter("Adapter B", "10.0.0.9"),
                Adapter("Adapter A", "192.168.68.57"),
            ], includeLoopback: false);

            CollectionAssert.AreEqual(
                new[] { "192.168.68.57", "10.0.0.9", "172.16.4.4" }, gewaehlt,
                "Gewaehlt wurde: " + string.Join(", ", gewaehlt));
        }

        /// <summary>
        /// <b>Die Gegenprobe:</b> ohne den Gateway-Riegel käme die falsche Adresse durch.
        /// <para>
        /// Sie ist der Beleg, dass die Zusicherungen oben etwas messen - ein Picker, der
        /// <i>alles</i> zurückgibt, würde sie sonst teilweise erfüllen.
        /// </para>
        /// </summary>
        [TestMethod]
        public void WithoutAnyUsableAdapterNothingIsOffered()
        {
            var gewaehlt = NetworkAddressPicker.Pick(
            [
                Adapter("Hyper-V Virtual Ethernet Adapter", "172.20.1.1"),
                Adapter("Ethernet (gezogen)", "192.168.1.10", up: false),
                Adapter("Intel Ethernet Connection", "192.168.10.5", gateway: false),
            ], includeLoopback: false);

            Assert.AreEqual(0, gewaehlt.Length,
                "Kein Adapter taugt - dann darf auch keine Adresse angeboten werden, statt "
                + "einer, die kein Telefon erreicht. Gewaehlt: " + string.Join(", ", gewaehlt));
        }

        /// <summary>Die Rückschleife kommt nur, wenn sie ausdrücklich verlangt wird.</summary>
        [TestMethod]
        public void LoopbackOnlyOnRequest()
        {
            NetworkAdapterInfo[] adapter =
            [
                new("Ethernet 2", true, false, false, true, ["127.0.0.1", "192.168.68.57"]),
            ];

            CollectionAssert.DoesNotContain(
                NetworkAddressPicker.Pick(adapter, includeLoopback: false), "127.0.0.1");

            CollectionAssert.Contains(
                NetworkAddressPicker.Pick(adapter, includeLoopback: true), "127.0.0.1");
        }

        /// <summary>Dieselbe Adresse auf zwei Adaptern erscheint einmal.</summary>
        [TestMethod]
        public void DuplicatesAppearOnce()
        {
            var gewaehlt = NetworkAddressPicker.Pick(
            [
                Adapter("Ethernet 2", "192.168.68.57"),
                Adapter("Ethernet 3", "192.168.68.57"),
            ], includeLoopback: false);

            Assert.AreEqual(1, gewaehlt.Length,
                "Gewaehlt wurde: " + string.Join(", ", gewaehlt));
        }
    }
}
