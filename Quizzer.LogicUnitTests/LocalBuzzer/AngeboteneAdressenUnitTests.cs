using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.TestShared;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Welche Adressen der Server den Telefonen anbietet (V14). Eine feste Bindung bietet nur sich selbst an,
    /// eine auf alle Adressen die Adressen dieses Rechners. Der Testserver bindet an 127.0.0.1 und sagt das.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class AngeboteneAdressenUnitTests
    {
        private const int TestPort = TestPorts.AngeboteneAdressen;

        [TestMethod]
        public async Task TheTestServerListensOnLoopbackOnlyAndSaysSo()
        {
            await using var server = new BuzzerServer();
            await HubTest.StartAsync(server, TestPort);

            var erwartet = $"{TestNetwork.Address}:{TestPort}";
            var gemeldet = server.GetListeningIpPorts(includeLoopback: true);

            CollectionAssert.AreEqual(new[] { erwartet }, gemeldet.ToArray(),
                $"Der Testserver meldet [{string.Join(", ", gemeldet)}] statt nur {erwartet}.");
            Assert.AreEqual(erwartet, server.GetBestListeningIpPort());
        }

        [TestMethod]
        public void AServerOnAllAddressesOffersTheAddressesOfThisMachine()
        {
            var angeboten = BuzzerServer.ListeningIpPortsFor(
                [$"http://{BuzzerServerOptions.AnyAddress}:5000"], () => ["192.168.68.57", "10.0.0.4"]);

            CollectionAssert.AreEqual(new[] { "10.0.0.4:5000", "192.168.68.57:5000" }, angeboten.ToArray(),
                $"Angeboten: [{string.Join(", ", angeboten)}]");
        }

        [TestMethod]
        public void AServerOnOneAddressOffersOnlyThatOne()
        {
            var gefragt = false;
            var angeboten = BuzzerServer.ListeningIpPortsFor([$"http://{TestNetwork.Address}:5000"], () =>
            {
                gefragt = true;
                return ["192.168.68.57"];
            });

            CollectionAssert.AreEqual(new[] { $"{TestNetwork.Address}:5000" }, angeboten.ToArray(),
                $"Angeboten: [{string.Join(", ", angeboten)}]");
            Assert.IsFalse(gefragt, "Für eine feste Bindung werden die Adressen des Rechners gelesen.");
        }
    }
}
