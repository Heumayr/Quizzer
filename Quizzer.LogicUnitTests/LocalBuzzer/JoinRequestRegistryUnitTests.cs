using LocalBuzzer.Service.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// M10: Beitrittsanfragen vom Telefon. Die Liste steht im WLAN des Spielorts offen - sie darf
    /// sich weder fluten lassen noch Namen annehmen, die es nicht gibt.
    /// </summary>
    [TestClass]
    public sealed class JoinRequestRegistryUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();

        private static readonly IReadOnlyList<JoinCandidate> Kandidaten = [new(Anna, "Anna")];

        [TestMethod]
        public void AKnownNameBecomesAPendingRequestWithItsDisplayName()
        {
            var registry = new JoinRequestRegistry();
            JoinRequest? gemeldet = null;
            registry.Requested += r => gemeldet = r;

            var (ergebnis, anfrage) = registry.Create(Anna, null, Kandidaten, "10.0.0.5");

            Assert.AreEqual(JoinRequestRegistry.CreateResult.Created, ergebnis);
            Assert.AreEqual("Anna", anfrage!.DisplayName);
            Assert.AreSame(anfrage, gemeldet, "Der Laptop erfährt sofort von der Anfrage.");
            Assert.AreEqual(1, registry.Pending().Count);
        }

        [TestMethod]
        public void AnUnknownPlayerIdIsRefused()
        {
            var registry = new JoinRequestRegistry();

            var (ergebnis, _) = registry.Create(Guid.NewGuid(), null, Kandidaten, "10.0.0.5");

            Assert.AreEqual(JoinRequestRegistry.CreateResult.Invalid, ergebnis);
            Assert.AreEqual(0, registry.Pending().Count);
        }

        [TestMethod]
        public void AGuestNeedsASensibleName()
        {
            var registry = new JoinRequestRegistry();

            Assert.AreEqual(JoinRequestRegistry.CreateResult.Invalid, registry.Create(null, "   ", Kandidaten, "a").Result);
            Assert.AreEqual(JoinRequestRegistry.CreateResult.Invalid, registry.Create(null, new string('x', 41), Kandidaten, "a").Result);
            Assert.AreEqual(JoinRequestRegistry.CreateResult.Invalid, registry.Create(null, "Bo\u0007b", Kandidaten, "a").Result);
            Assert.AreEqual(JoinRequestRegistry.CreateResult.Created, registry.Create(null, " Carla ", Kandidaten, "a").Result);
            Assert.AreEqual("Carla", registry.Pending()[0].DisplayName);
        }

        [TestMethod]
        public void OnePhoneCannotFloodTheList()
        {
            var registry = new JoinRequestRegistry();

            for (var i = 0; i < JoinRequestRegistry.MaxPendingPerAddress; i++)
                Assert.AreEqual(JoinRequestRegistry.CreateResult.Created, registry.Create(null, "Gast " + i, Kandidaten, "10.0.0.9").Result);

            Assert.AreEqual(JoinRequestRegistry.CreateResult.TooMany, registry.Create(null, "noch einer", Kandidaten, "10.0.0.9").Result);
            Assert.AreEqual(JoinRequestRegistry.CreateResult.Created, registry.Create(null, "anderes Telefon", Kandidaten, "10.0.0.10").Result);
        }

        [TestMethod]
        public void AcceptAndDeclineWorkOnlyOnce()
        {
            var registry = new JoinRequestRegistry();
            var anfrage = registry.Create(Anna, null, Kandidaten, "a").Request!;

            Assert.IsTrue(registry.Accept(anfrage.Id, Anna));
            Assert.IsFalse(registry.Decline(anfrage.Id), "Eine angenommene Anfrage lässt sich nicht mehr ablehnen.");
            Assert.AreEqual(JoinStatus.Accepted, registry.Get(anfrage.Id)!.Status);
            Assert.AreEqual(Anna, registry.Get(anfrage.Id)!.AssignedPlayerId);
        }

        [TestMethod]
        public void AnUnansweredRequestExpires()
        {
            var uhr = new VerstellbareUhr();
            var registry = new JoinRequestRegistry(uhr);
            var anfrage = registry.Create(Anna, null, Kandidaten, "a").Request!;

            uhr.Jetzt += JoinRequestRegistry.Lifetime + TimeSpan.FromSeconds(1);

            Assert.AreEqual(JoinStatus.Expired, registry.Get(anfrage.Id)!.Status);
            Assert.AreEqual(0, registry.Pending().Count);
            Assert.IsFalse(registry.Accept(anfrage.Id, Anna), "Zu spät angenommen gilt nicht.");
        }

        private sealed class VerstellbareUhr : TimeProvider
        {
            public DateTimeOffset Jetzt { get; set; } = new(2026, 10, 21, 20, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => Jetzt;
        }
    }
}
