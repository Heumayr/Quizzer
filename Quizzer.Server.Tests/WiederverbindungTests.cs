using Quizzer.Client;

namespace Quizzer.Server.Tests
{
    /// <summary>Der Wächter, der eine offline begonnene Sitzung wieder verbindet (2026-10-22).</summary>
    [TestClass]
    public sealed class WiederverbindungTests
    {
        private static readonly TimeSpan Kurz = TimeSpan.FromMilliseconds(10);

        [TestMethod]
        public async Task ItKeepsTryingUntilTheServerIsBackAndThenStops()
        {
            var versuche = 0;
            var ergebnisse = new List<WiederverbindungsErgebnis>();

            using var w = new Wiederverbindung(_ =>
            {
                versuche++;
                return Task.FromResult(versuche < 4 ? WiederverbindungsErgebnis.NichtErreichbar : WiederverbindungsErgebnis.Verbunden);
            }, Kurz, Kurz);

            w.Beendet += (_, e) => ergebnisse.Add(e);
            w.Start();
            await w.Fertig.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.AreEqual(4, versuche);
            CollectionAssert.AreEqual(new[] { WiederverbindungsErgebnis.Verbunden }, ergebnisse, "Genau einmal melden.");
        }

        [TestMethod]
        public async Task AnExpiredSignInEndsTheWatchToo()
        {
            var ergebnisse = new List<WiederverbindungsErgebnis>();
            using var w = new Wiederverbindung(_ => Task.FromResult(WiederverbindungsErgebnis.AnmeldungNoetig), Kurz, Kurz);

            w.Beendet += (_, e) => ergebnisse.Add(e);
            w.Start();
            await w.Fertig.WaitAsync(TimeSpan.FromSeconds(10));

            CollectionAssert.AreEqual(new[] { WiederverbindungsErgebnis.AnmeldungNoetig }, ergebnisse);
        }

        /// <summary>Ein unerwarteter Fehler im Versuch zählt als „noch nicht da" - der Abend läuft weiter.</summary>
        [TestMethod]
        public async Task AnExceptionIsJustAnotherFailedAttempt()
        {
            var versuche = 0;
            using var w = new Wiederverbindung(_ =>
            {
                if (++versuche == 1)
                    throw new InvalidOperationException("Datenbank kurz weg");
                return Task.FromResult(WiederverbindungsErgebnis.Verbunden);
            }, Kurz, Kurz);

            w.Start();
            await w.Fertig.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.AreEqual(2, versuche);
        }

        [TestMethod]
        public async Task DisposingStopsTheWatchWithoutAnEvent()
        {
            var gemeldet = false;
            var w = new Wiederverbindung(_ => Task.FromResult(WiederverbindungsErgebnis.NichtErreichbar), TimeSpan.FromMinutes(5));
            w.Beendet += (_, _) => gemeldet = true;
            w.Start();

            w.Dispose();
            await w.Fertig.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.IsFalse(gemeldet);
        }

        [TestMethod]
        public void TheIntervalsAreReasonableForAnEvening()
        {
            using var w = new Wiederverbindung(_ => Task.FromResult(WiederverbindungsErgebnis.Verbunden));

            Assert.AreEqual(TimeSpan.FromSeconds(20), w.Erster);
            Assert.AreEqual(TimeSpan.FromMinutes(2), w.Hoechstens, "Laenger als zwei Minuten soll ein zurueckgekehrtes WLAN nicht ungenutzt bleiben.");
        }
    }
}
