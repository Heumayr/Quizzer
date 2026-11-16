using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Der Zocker am Server (M35 C4) ohne Netz: die Serveruhr entscheidet Ausstieg gegen Absturz, der
    /// Absturzpunkt steht in keinem Zustand, solange die Runde laeuft, und nur wer beim Start da war, spielt.
    /// </summary>
    [TestClass]
    public class ZockerStateUnitTests
    {
        /// <summary>Wurf fuer den Absturz bei ×2,40.</summary>
        private const int Wurf240 = 5834;

        /// <summary>Wurf fuer ×7,77 - eine Zahl, die sonst nirgends im Zustand steht.</summary>
        private const int Wurf777 = 8713;

        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);

        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private StellbareUhr uhr = null!;
        private MinigameState spiel = null!;
        private int wurf = Wurf240;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");
            uhr = new StellbareUhr();

            var game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { anna, bert, carl })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            spiel = new MinigameState(new GameAccessor { GetGame = () => game }) { Zufall = (_, _) => wurf, Uhr = uhr };
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private CrashRules Regeln => (CrashRules)spiel.Rules!;

        private void Starte()
        {
            spiel.Configure(new MinigameSetup(MinigameKind.Crash, "Zocker", "Regel", 1));
            spiel.Reset();
            Assert.IsTrue(spiel.StartNextRound(), "Die Runde liess sich nicht starten.");
        }

        private MinigameSubmitResult Gib(Player p, object nutzlast, MinigameKind art = MinigameKind.Crash, int? durchgang = null)
            => spiel.Submit(p, new MinigameAction
            {
                Kind = art,
                Durchgang = durchgang ?? spiel.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private MinigameSubmitResult SteigAus(Player p) => Gib(p, new { cashOut = true });

        private CrashClientInfo Art(Player? p) => (CrashClientInfo)((MinigameClientInfo)spiel.InfoFor(p)).KindInfo!;

        private void NachDemStart(TimeSpan laufzeit) => uhr.Vor(Vorlauf + laufzeit);

        [TestMethod]
        public void ACashOutCountsWithTheMultiplierOfTheServerClock()
        {
            Starte();
            NachDemStart(TimeSpan.FromSeconds(2));

            var ergebnis = SteigAus(anna);

            Assert.IsTrue(ergebnis.Accepted, "Ein Ausstieg vor dem Absturz wurde verworfen.");
            Assert.AreEqual(new ZockerAusstieg(ZockerEvaluator.Multiplikator(TimeSpan.FromSeconds(2))), ergebnis.Value);
            Assert.AreEqual(128, Art(anna).OwnMultiplier, "Der eigene Ausstieg steht nicht im Zustand des Telefons.");
        }

        [TestMethod]
        public void DuringTheCountdownNothingIsAccepted()
        {
            Starte();
            uhr.Vor(TimeSpan.FromSeconds(1));

            Assert.IsFalse(SteigAus(anna).Accepted, "Im Vorlauf wurde ein Ausstieg angenommen.");
        }

        /// <summary>Die Abgabe kommt nach dem Absturz an, die Runde ist aber noch nicht geschlossen - die Uhr entscheidet.</summary>
        [TestMethod]
        public void ACashOutAfterTheCrashDoesNotCountEvenBeforeTheRoundIsClosed()
        {
            Starte();
            NachDemStart(ZockerEvaluator.Absturzzeit(240) + TimeSpan.FromMilliseconds(1));

            Assert.AreEqual(MinigamePhase.Running, spiel.Phase, "Die Runde ist schon zu - die Probe prueft dann den Rahmen, nicht die Uhr.");
            Assert.IsFalse(SteigAus(bert).Accepted, "Ein Ausstieg nach dem Absturz wurde angenommen.");

            var zu = spiel.CloseIfDue();

            Assert.IsNotNull(zu, "Nach dem Absturz schliesst die Runde nicht.");
            Assert.IsTrue(zu.Finished);
            Assert.AreEqual(0, zu.Submissions.Count);
            Assert.IsTrue(Art(bert).Crashed);
            Assert.AreEqual(240, Art(bert).CrashMultiplier);
            Assert.IsNull(Art(bert).OwnMultiplier);
        }

        [TestMethod]
        public void TheRoundClosesByItselfOnlyAtTheCrash()
        {
            Starte();
            NachDemStart(ZockerEvaluator.Absturzzeit(240) - TimeSpan.FromMilliseconds(5));

            Assert.IsNull(spiel.CloseIfDue(), "Die Runde schliesst vor dem Absturz.");
            Assert.IsTrue(SteigAus(anna).Accepted, "Kurz vor dem Absturz zaehlt der Ausstieg nicht.");

            uhr.Vor(TimeSpan.FromMilliseconds(10));

            Assert.IsNotNull(spiel.CloseIfDue());
            Assert.AreEqual(MinigamePhase.Finished, spiel.Phase);
            Assert.IsTrue(Regeln.Abgestuerzt);
        }

        [TestMethod]
        public void ASecondAForeignOrABrokenSubmissionIsRejected()
        {
            Starte();
            NachDemStart(TimeSpan.FromSeconds(1));

            Assert.IsFalse(Gib(anna, new { cashOut = false }).Accepted, "„Nicht aussteigen“ wurde als Ausstieg genommen.");
            Assert.IsFalse(Gib(anna, new { }).Accepted, "Eine leere Nutzlast wurde angenommen.");
            Assert.IsFalse(Gib(anna, 42).Accepted, "Eine Zahl als Nutzlast wurde angenommen.");
            Assert.IsFalse(Gib(anna, new { cashOut = true }, MinigameKind.Reaction).Accepted, "Eine fremde Art wurde angenommen.");
            Assert.IsFalse(Gib(anna, new { cashOut = true }, durchgang: spiel.Durchgang - 1).Accepted, "Ein alter Durchgang wurde angenommen.");

            Assert.IsTrue(SteigAus(anna).Accepted);
            uhr.Vor(TimeSpan.FromSeconds(1));
            Assert.IsFalse(SteigAus(anna).Accepted, "Ein zweiter Ausstieg wurde angenommen.");
            Assert.AreEqual(ZockerEvaluator.Multiplikator(TimeSpan.FromSeconds(1)), Art(anna).OwnMultiplier, "Der zweite Ausstieg hat den ersten ueberschrieben.");
        }

        /// <summary>Wer beim Start fehlte, kommt nicht mitten in der Runde dazu - er saehe den Multiplikator schon steigen.</summary>
        [TestMethod]
        public void OnlyThoseConnectedAtTheStartPlayAndTheRoundClosesWhenTheyAreOut()
        {
            carl.ConnectionState = PlayerConnection.Disconnected;
            Starte();
            carl.ConnectionState = PlayerConnection.Connected;
            NachDemStart(TimeSpan.FromSeconds(1));

            Assert.IsTrue(spiel.IsLockedFor(carl), "Wer beim Start fehlte, ist nicht gesperrt.");
            Assert.IsFalse(SteigAus(carl).Accepted, "Wer beim Start fehlte, ist ausgestiegen.");
            CollectionAssert.AreEquivalent(new[] { anna.Id, bert.Id }, Regeln.Teilnehmer.ToList());

            Assert.IsNull(SteigAus(anna).Closed);
            Assert.IsNotNull(SteigAus(bert).Closed, "Alle Teilnehmer sind draussen, die Runde laeuft weiter.");
            Assert.IsFalse(Regeln.Abgestuerzt, "Alle waren draussen - abgestuerzt ist nichts.");
            Assert.AreEqual(240, Art(anna).CrashMultiplier, "Nach der Runde steht nicht da, wo es abgestuerzt waere.");
        }

        /// <summary>Ein Telefon, das wiederkommt, bekommt seinen Ausstieg zurueck - und keinen zweiten Versuch.</summary>
        [TestMethod]
        public void AReconnectingPhoneGetsItsCashOutBack()
        {
            Starte();
            NachDemStart(TimeSpan.FromSeconds(3));
            Assert.IsTrue(SteigAus(anna).Accepted);

            var info = (MinigameClientInfo)spiel.InfoFor(anna);

            Assert.IsTrue(info.OwnSubmitted, "Nach dem Wiederverbinden darf das Telefon noch einmal.");
            Assert.AreEqual(ZockerEvaluator.Multiplikator(TimeSpan.FromSeconds(3)), ((CrashClientInfo)info.KindInfo!).OwnMultiplier);
            Assert.IsTrue(spiel.IsLockedFor(anna));
        }

        /// <summary>Kein Zustand - kein Telefon, nicht der Blick von aussen, nicht der Beamer - traegt den Absturzpunkt vor dem Absturz.</summary>
        [TestMethod]
        public void TheCrashPointIsInNoStateWhileTheRoundRuns()
        {
            wurf = Wurf777;
            Starte();

            foreach (var schritt in new[] { TimeSpan.Zero, Vorlauf, TimeSpan.FromSeconds(5), ZockerEvaluator.Absturzzeit(777) - TimeSpan.FromSeconds(5) - TimeSpan.FromMilliseconds(1) })
            {
                uhr.Vor(schritt);

                if (schritt == TimeSpan.FromSeconds(5))
                    Assert.IsTrue(SteigAus(anna).Accepted);

                foreach (var p in new[] { anna, bert, carl, null })
                {
                    Assert.IsNull(Art(p).CrashMultiplier, "Der Absturzpunkt steht im Zustand eines Telefons.");
                    CollectionAssert.DoesNotContain(Zahlen(spiel.InfoFor(p)), 777.0, "Der Absturzpunkt steckt im Zustand.");
                }

                Assert.AreNotEqual(777, Regeln.Stand.Multiplikator, "Der Beamer sieht den Absturzpunkt vor dem Absturz.");
                Assert.IsNull(Regeln.Aufgedeckt);
            }

            uhr.Vor(TimeSpan.FromMilliseconds(2));
            Assert.AreEqual(new CrashStand(777, true, true), Regeln.Stand, "Nach dem Absturz zeigt der Beamer ihn nicht.");
            Assert.IsNotNull(spiel.CloseIfDue());
            CollectionAssert.Contains(Zahlen(spiel.InfoFor(bert)), 777.0, "Nach der Runde erfaehrt das Telefon den Absturzpunkt nicht - die Probe misst sonst nichts.");
        }

        [TestMethod]
        public void AnAbortKeepsTheCrashPointSecret()
        {
            Starte();
            NachDemStart(TimeSpan.FromSeconds(1));

            Assert.IsNotNull(spiel.Abort());

            Assert.IsNull(Regeln.Aufgedeckt);
            Assert.IsNull(Art(anna).CrashMultiplier);
        }

        /// <summary>Alle Zahlen im Zustand, so wie der Hub ihn ans Telefon schickt.</summary>
        private static List<double> Zahlen(object zustand)
        {
            var zahlen = new List<double>();
            Sammle(JsonSerializer.SerializeToElement(zustand, new JsonSerializerOptions(JsonSerializerDefaults.Web)), zahlen);
            return zahlen;
        }

        private static void Sammle(JsonElement e, List<double> zahlen)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Number:
                    zahlen.Add(e.GetDouble());
                    break;
                case JsonValueKind.Object:
                    foreach (var p in e.EnumerateObject())
                        Sammle(p.Value, zahlen);
                    break;
                case JsonValueKind.Array:
                    foreach (var x in e.EnumerateArray())
                        Sammle(x, zahlen);
                    break;
            }
        }
    }
}
