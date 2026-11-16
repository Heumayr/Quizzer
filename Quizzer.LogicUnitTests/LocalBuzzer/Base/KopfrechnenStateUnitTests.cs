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
    /// Das Kopfrechnen am Server (M35 C5) ohne Netz: jede Antwort wird an der Serveruhr geprueft und bringt
    /// die naechste Aufgabe fuer genau dieses Telefon, alle rechnen dieselbe Folge, und keine Loesung steht
    /// in einem Zustand.
    /// </summary>
    [TestClass]
    public class KopfrechnenStateUnitTests
    {
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan Dauer = TimeSpan.FromSeconds(30);

        private Player anna = null!;
        private Player bert = null!;
        private StellbareUhr uhr = null!;
        private MinigameState spiel = null!;
        private int startwert = 4711;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            uhr = new StellbareUhr();

            var game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { anna, bert })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            spiel = new MinigameState(new GameAccessor { GetGame = () => game }) { Zufall = (_, _) => startwert, Uhr = uhr };
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private void Starte(int stufe = KopfrechnenAufgaben.Mittel)
        {
            spiel.Configure(new MinigameSetup(MinigameKind.MentalArithmetic, "Kopfrechnen", "Regel", 1,
                DurationSeconds: (int)Dauer.TotalSeconds, Variant: stufe));
            spiel.Reset();
            Assert.IsTrue(spiel.StartNextRound(), "Die Runde liess sich nicht starten.");
        }

        private MinigameSubmitResult Gib(Player p, object nutzlast, MinigameKind art = MinigameKind.MentalArithmetic)
            => spiel.Submit(p, new MinigameAction
            {
                Kind = art,
                Durchgang = spiel.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private MentalArithmeticClientInfo Art(Player? p) => (MentalArithmeticClientInfo)((MinigameClientInfo)spiel.InfoFor(p)).KindInfo!;

        private int Loesung(int nummer, int stufe = KopfrechnenAufgaben.Mittel) => KopfrechnenAufgaben.Aufgabe(startwert, nummer, stufe).Loesung;

        /// <summary>Beantwortet die laufende Aufgabe dieses Spielers; mit Abstand, damit die Grenze nicht greift.</summary>
        private MinigameSubmitResult Antworte(Player p, bool richtig)
        {
            uhr.Vor(TimeSpan.FromMilliseconds(400));
            var nummer = Art(p).TaskNumber;
            return Gib(p, new { taskNumber = nummer, answer = Loesung(nummer) + (richtig ? 0 : 1) });
        }

        [TestMethod]
        public void ARightAndAWrongAnswerCountAndBothBringTheNextTask()
        {
            Starte();
            uhr.Vor(Vorlauf);

            var erste = Antworte(anna, richtig: true);
            Assert.IsTrue(erste.Accepted, "Eine richtige Antwort wurde verworfen.");
            Assert.AreEqual(new KopfrechnenAntwort(1, true, new KopfrechnenStand(1, 1)), erste.Value);

            var zweite = Antworte(anna, richtig: false);
            Assert.IsTrue(zweite.Accepted, "Eine falsche Antwort wurde verworfen - sie kostet nur Zeit.");
            Assert.AreEqual(new KopfrechnenAntwort(2, false, new KopfrechnenStand(1, 2)), zweite.Value);

            var info = Art(anna);
            Assert.AreEqual(3, info.TaskNumber, "Nach zwei Antworten steht nicht die dritte Aufgabe da.");
            Assert.AreEqual(KopfrechnenAufgaben.Aufgabe(startwert, 3, KopfrechnenAufgaben.Mittel).Text, info.TaskText);
            Assert.AreEqual(1, info.OwnCorrect);
            Assert.AreEqual(2, info.OwnAnswered);
            Assert.AreEqual(false, info.LastCorrect);
            Assert.IsNull(erste.Closed ?? zweite.Closed, "Die Runde schliesst vor der Frist.");
            Assert.IsFalse(spiel.IsLockedFor(anna), "Nach einer Antwort ist das Telefon gesperrt.");
        }

        [TestMethod]
        public void DuringTheCountdownNothingIsAccepted()
        {
            Starte();
            uhr.Vor(TimeSpan.FromSeconds(1));

            Assert.IsFalse(Gib(anna, new { taskNumber = 1, answer = Loesung(1) }).Accepted, "Im Vorlauf wurde eine Antwort angenommen.");
        }

        /// <summary>Die Antwort kommt nach der Frist an, die Runde ist aber noch nicht geschlossen - die Uhr entscheidet.</summary>
        [TestMethod]
        public void AnAnswerAfterTheDeadlineDoesNotCountAndTheRoundClosesOnlyThen()
        {
            Starte();
            uhr.Vor(Vorlauf + Dauer - TimeSpan.FromMilliseconds(5));

            Assert.IsNull(spiel.CloseIfDue(), "Die Runde schliesst vor der Frist.");
            Assert.IsTrue(Gib(anna, new { taskNumber = 1, answer = Loesung(1) }).Accepted, "Kurz vor der Frist zaehlt die Antwort nicht.");

            uhr.Vor(TimeSpan.FromMilliseconds(10));
            Assert.AreEqual(MinigamePhase.Running, spiel.Phase, "Die Runde ist schon zu - die Probe prueft dann den Rahmen, nicht die Uhr.");
            Assert.IsFalse(Gib(bert, new { taskNumber = 1, answer = Loesung(1) }).Accepted, "Eine Antwort nach der Frist wurde angenommen.");

            var zu = spiel.CloseIfDue();

            Assert.IsNotNull(zu, "Nach der Frist schliesst die Runde nicht.");
            Assert.IsTrue(zu.Finished);
            Assert.AreEqual(new KopfrechnenStand(1, 1), zu.Submissions[anna.Id]);
            Assert.IsFalse(zu.Submissions.ContainsKey(bert.Id), "Die spaete Antwort steht in der Wertung.");
            Assert.IsTrue(Art(anna).IsWinner);
            CollectionAssert.AreEqual(new[] { "Anna" }, Art(bert).WinnerNames);
        }

        [TestMethod]
        public void AnOldTaskAForeignKindOrABrokenPayloadIsRejected()
        {
            Starte();
            uhr.Vor(Vorlauf);

            Assert.IsFalse(Gib(anna, new { taskNumber = 1 }).Accepted, "Eine Abgabe ohne Antwort wurde angenommen.");
            Assert.IsFalse(Gib(anna, 42).Accepted, "Eine Zahl als Nutzlast wurde angenommen.");
            Assert.IsFalse(Gib(anna, new { taskNumber = 1, answer = Loesung(1) }, MinigameKind.TapSprint).Accepted, "Eine fremde Art wurde angenommen.");
            Assert.IsFalse(Gib(anna, new { taskNumber = 2, answer = Loesung(2) }).Accepted, "Eine kuenftige Aufgabe wurde angenommen.");

            Assert.IsTrue(Gib(anna, new { taskNumber = 1, answer = Loesung(1) }).Accepted);
            uhr.Vor(TimeSpan.FromSeconds(1));
            Assert.IsFalse(Gib(anna, new { taskNumber = 1, answer = Loesung(1) }).Accepted, "Dieselbe Aufgabe zaehlte zweimal.");
            Assert.AreEqual(1, Art(anna).OwnCorrect, "Die doppelte Antwort hat den Stand veraendert.");
        }

        /// <summary>Mehr als drei Antworten je Sekunde schafft kein Mensch - die vierte wird verworfen, nach der Sekunde geht es weiter.</summary>
        [TestMethod]
        public void MoreAnswersPerSecondThanTheLimitAreRejected()
        {
            Starte();
            uhr.Vor(Vorlauf);

            for (var i = 0; i < KopfrechnenEvaluator.MaxAntwortenJeSekunde; i++)
            {
                uhr.Vor(TimeSpan.FromMilliseconds(10));
                var nummer = Art(anna).TaskNumber;
                Assert.IsTrue(Gib(anna, new { taskNumber = nummer, answer = Loesung(nummer) }).Accepted, $"Antwort {i + 1} wurde verworfen.");
            }

            var naechste = Art(anna).TaskNumber;
            Assert.IsFalse(Gib(anna, new { taskNumber = naechste, answer = Loesung(naechste) }).Accepted, "Die Grenze greift nicht.");
            Assert.AreEqual(naechste, Art(anna).TaskNumber, "Eine verworfene Antwort hat die Aufgabe weitergeschaltet.");

            uhr.Vor(TimeSpan.FromSeconds(1));
            Assert.IsTrue(Gib(anna, new { taskNumber = naechste, answer = Loesung(naechste) }).Accepted, "Nach der Sekunde geht es nicht weiter.");
        }

        [TestMethod]
        public void EveryoneGetsTheSameSequence()
        {
            Starte();
            uhr.Vor(Vorlauf);

            var annasFolge = new List<string?>();
            for (var i = 0; i < 4; i++)
            {
                annasFolge.Add(Art(anna).TaskText);
                Assert.IsTrue(Antworte(anna, richtig: i % 2 == 0).Accepted);
            }

            var bertsFolge = new List<string?>();
            for (var i = 0; i < 4; i++)
            {
                bertsFolge.Add(Art(bert).TaskText);
                Assert.IsTrue(Antworte(bert, richtig: true).Accepted);
            }

            CollectionAssert.AreEqual(annasFolge, bertsFolge, "Bert rechnet andere Aufgaben als Anna.");
            Assert.AreEqual(4, annasFolge.Distinct().Count(), "Die Folge besteht aus derselben Aufgabe - die Probe misst nichts.");
        }

        /// <summary>Ein Telefon, das wiederkommt, bekommt seine Aufgabe und seinen Stand zurueck - es faengt nicht von vorn an.</summary>
        [TestMethod]
        public void AReconnectingPhoneGetsItsTaskAndCountBack()
        {
            Starte();
            uhr.Vor(Vorlauf);
            Antworte(anna, richtig: true);
            Antworte(anna, richtig: true);

            anna.ConnectionState = PlayerConnection.Disconnected;
            Assert.IsNull(spiel.CloseIfComplete(), "Nach der Trennung schliesst die Runde - beim Kopfrechnen laeuft sie bis zur Frist.");
            anna.ConnectionState = PlayerConnection.Connected;

            var info = (MinigameClientInfo)spiel.InfoFor(anna);
            var art = (MentalArithmeticClientInfo)info.KindInfo!;

            Assert.IsFalse(info.OwnSubmitted, "Das Telefon gilt als fertig und darf nicht weiterrechnen.");
            Assert.AreEqual(3, art.TaskNumber);
            Assert.AreEqual(2, art.OwnCorrect);
            Assert.IsTrue(art.RemainingMs is > 0 and < 30_000, "Die Restzeit fehlt oder kommt nicht von der Serveruhr: " + art.RemainingMs);
        }

        /// <summary>
        /// Keine Loesung, kein Startwert und keine kuenftige Aufgabe in irgendeinem Zustand - kein Telefon, nicht
        /// der Blick von aussen -, in keiner Phase.
        /// </summary>
        [TestMethod]
        public void NoSolutionIsInAnyState()
        {
            // Startwert und erste Loesungen gross und ungewoehnlich - eine kleine Zahl stuende zufaellig als Zaehler da.
            startwert = Enumerable.Range(100_000, 10_000_000).First(s => s % 1000 == 777
                && Enumerable.Range(1, 8).All(n => KopfrechnenAufgaben.Aufgabe(s, n, KopfrechnenAufgaben.Schwer).Loesung > 100));
            var geheim = Enumerable.Range(1, 8).Select(n => (double)Loesung(n, KopfrechnenAufgaben.Schwer)).Append(startwert).ToList();
            var kuenftig = Enumerable.Range(4, 5).Select(n => KopfrechnenAufgaben.Aufgabe(startwert, n, KopfrechnenAufgaben.Schwer).Text).ToList();

            Pruefe("vor dem Start");
            Starte(KopfrechnenAufgaben.Schwer);
            Pruefe("im Vorlauf");
            uhr.Vor(Vorlauf);

            for (var i = 0; i < 2; i++)
            {
                uhr.Vor(TimeSpan.FromMilliseconds(400));
                var nummer = Art(anna).TaskNumber;
                Assert.IsTrue(Gib(anna, new { taskNumber = nummer, answer = (int)geheim[nummer - 1] }).Accepted);
                Pruefe($"nach Antwort {i + 1}");
            }

            Assert.IsTrue(Zustandsinhalt.Von(spiel.InfoFor(anna)).EnthaeltText(KopfrechnenAufgaben.Aufgabe(startwert, 3, KopfrechnenAufgaben.Schwer).Text),
                "Die laufende Aufgabe steht nicht im Zustand - die Probe misst sonst nichts.");

            uhr.Vor(Dauer);
            Assert.IsNotNull(spiel.CloseIfDue());
            Pruefe("nach der Runde");

            void Pruefe(string wann)
            {
                foreach (var p in new[] { anna, bert, null })
                {
                    var inhalt = Zustandsinhalt.Von(spiel.InfoFor(p));

                    foreach (var zahl in geheim)
                        CollectionAssert.DoesNotContain(inhalt.Zahlen, zahl, $"{wann}: eine Loesung oder der Startwert steht im Zustand ({zahl}).");

                    foreach (var text in kuenftig)
                        Assert.IsFalse(inhalt.EnthaeltText(text), $"{wann}: eine kuenftige Aufgabe steht im Zustand ({text}).");
                }

                var aussen = ((MinigameClientInfo)spiel.InfoFor(null)).KindInfo as MentalArithmeticClientInfo;
                Assert.IsNull(aussen?.TaskText, $"{wann}: der Blick von aussen sieht eine Aufgabe.");
            }
        }
    }
}
