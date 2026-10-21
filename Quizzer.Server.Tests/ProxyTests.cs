using Quizzer.Server.Infrastructure;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Caddy vor der API (2026-10-21): welchen Netzen die API <c>X-Forwarded-For</c> glaubt.
    /// Der Weg durch einen echten Caddy ist am selben Tag von Hand gemessen worden (zweiter
    /// Absender bekam 401 statt 429) - hier steht, was ohne Container prüfbar ist.
    /// </summary>
    [TestClass]
    public sealed class ProxyTests
    {
        [TestMethod]
        public void WithoutConfigurationNoHeaderIsTrusted()
        {
            Assert.AreEqual(0, ServerSetup.ParseProxyNetworks(null).Count);
            Assert.AreEqual(0, ServerSetup.ParseProxyNetworks("  ").Count);
        }

        [TestMethod]
        public void SeveralNetworksAreRead()
        {
            var netze = ServerSetup.ParseProxyNetworks("172.30.77.0/24, fd00::/8");

            Assert.AreEqual(2, netze.Count);
            Assert.IsTrue(netze[0].Contains(System.Net.IPAddress.Parse("172.30.77.5")));
            Assert.IsFalse(netze[0].Contains(System.Net.IPAddress.Parse("172.30.78.5")));
        }

        /// <summary>Ein Tippfehler bricht den Start ab, statt still allen gemeinsam zu bremsen.</summary>
        [TestMethod]
        [DataRow("172.30.77.0/33")]
        [DataRow("caddy")]
        [DataRow("172.30.77.0/24;10.0.0.0/8")]
        public void AnUnreadableNetworkStopsTheStart(string wert)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => ServerSetup.ParseProxyNetworks(wert));
        }
    }
}
