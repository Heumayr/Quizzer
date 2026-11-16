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
    /// Die Blitzrunde am Server (M35 C5) ohne Netz: jede Antwort bringt die naechste Aussage fuer genau dieses
    /// Telefon, alle beurteilen dieselbe gemischte Folge, und ob eine Aussage wahr ist, steht in keinem Zustand,
    /// solange die Runde laeuft.
    /// </summary>
    [TestClass]
    public class BlitzrundeStateUnitTests
    {
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan Dauer = TimeSpan.FromSeconds(60);

        // Vier wahr, zwei falsch: umgedreht ergibt „alles wahr“ ein anderes Ergebnis - sonst saehe die Gegenprobe nichts.
        private static readonly MinispielZeile[] Aussagen =
        [
            new("Der Mond ist ein Planet.", false),
            new("Wasser kocht auf Meereshöhe bei 100 °C.", true),
            new("Berlin liegt an der Spree.", true),
            new("Spinnen haben acht Beine.", true),
            new("Ein Jahr hat 52 Wochen und einen Tag.", true),
            new("Der Rhein mündet ins Schwarze Meer.", false),
        ];

        private Player anna = null!;
        private Player bert = null!;
        private Game game = null!;
        private StellbareUhr uhr = null!;
        private MinigameState spiel = null!;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            uhr = new StellbareUhr();

            game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { anna, bert })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            spiel = Neu();
        }

        private MinigameState Neu(int seed = 42) => new(new GameAccessor { GetGame = () => game }) { Zufall = new Random(seed).Next, Uhr = uhr };

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private static void Starte(MinigameState zustand, IReadOnlyList<MinispielZeile>? inhalt = null)
        {
            zustand.Configure(new MinigameSetup(MinigameKind.LightningRound, "Blitzrunde", "Regel", 1,
                DurationSeconds: (int)Dauer.TotalSeconds, Content: inhalt ?? Aussagen));
            zustand.Reset();
            Assert.IsTrue(zustand.StartNextRound(), "Die Runde liess sich nicht starten.");
        }

        private static MinigameSubmitResult Gib(MinigameState zustand, Player p, object nutzlast, MinigameKind art = MinigameKind.LightningRound)
            => zustand.Submit(p, new MinigameAction
            {
                Kind = art,
                Durchgang = zustand.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private MinigameSubmitResult Gib(Player p, object nutzlast, MinigameKind art = MinigameKind.LightningRound) => Gib(spiel, p, nutzlast, art);

        private static LightningRoundClientInfo Art(MinigameState zustand, Player? p)
            => (LightningRoundClientInfo)((MinigameClientInfo)zustand.InfoFor(p)).KindInfo!;

        private LightningRoundClientInfo Art(Player? p) => Art(spiel, p);

        private static bool Wahr(string? text) => Aussagen.Single(a => a.Text == text).Wahr;

        /// <summary>Beurteilt die laufende Aussage dieses Spielers; mit Abstand, damit die Grenze nicht greift.</summary>
        private MinigameSubmitResult Urteile(Player p, bool richtig)
        {
            uhr.Vor(TimeSpan.FromMilliseconds(400));
            var info = Art(p);
            return Gib(p, new { statementNumber = info.StatementNumber, answer = Wahr(info.StatementText) == richtig });
        }

        [TestMethod]
        public void AVerdictBringsTheNextStatementForThisPhoneOnlyAndSaysNothingAboutTheTruth()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            var ersteAussage = Art(anna).StatementText;
            var erste = Urteile(anna, richtig: false);

            Assert.IsTrue(erste.Accepted, "Ein Urteil wurde verworfen.");
            Assert.AreEqual(new BlitzrundeFortschritt(1, 1), erste.Value, "An den Spielleiter geht mehr als der Fortschritt.");

            var info = Art(anna);
            Assert.AreEqual(2, info.StatementNumber, "Nach einem Urteil steht nicht die zweite Aussage da.");
            Assert.AreNotEqual(ersteAussage, info.StatementText);
            Assert.AreEqual(1, info.OwnAnswered);
            Assert.IsNull(info.OwnCorrect, "Waehrend der Runde steht da, wie viele stimmten - das verraet die Aussage.");
            Assert.IsNull(info.OwnWrong);
            Assert.AreEqual(1, Art(bert).StatementNumber, "Berts Aussage ist mitgewandert.");
            Assert.IsNull(erste.Closed, "Die Runde schliesst vor der Frist.");
            Assert.IsFalse(spiel.IsLockedFor(anna), "Nach einem Urteil ist das Telefon gesperrt.");
        }

        /// <summary>Alle dieselbe Reihenfolge; die Wiederholung mischt neu - und gemischt ist ueberhaupt.</summary>
        [TestMethod]
        public void EveryoneJudgesTheSameOrderAndARepeatShufflesAnew()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            List<string?> Folge(Player p)
            {
                var folge = new List<string?>();
                for (var i = 0; i < Aussagen.Length; i++)
                {
                    folge.Add(Art(p).StatementText);
                    Assert.IsTrue(Urteile(p, richtig: true).Accepted);
                }
                return folge;
            }

            var annas = Folge(anna);
            CollectionAssert.AreEqual(annas, Folge(bert), "Bert beurteilt eine andere Folge als Anna.");
            CollectionAssert.AreEquivalent(Aussagen.Select(a => a.Text).ToList(), annas, "Nicht jede Aussage kam genau einmal.");
            CollectionAssert.AreNotEqual(Aussagen.Select(a => a.Text).ToList(), annas, "Die Folge ist nicht gemischt.");

            Assert.IsTrue(spiel.RepeatRound());
            uhr.Vor(Vorlauf);
            CollectionAssert.AreNotEqual(annas, Folge(anna), "Die Wiederholung mischt nicht neu.");
        }

        [TestMethod]
        public void DuringTheCountdownNothingIsAccepted()
        {
            Starte(spiel);
            uhr.Vor(TimeSpan.FromSeconds(1));

            Assert.IsFalse(Gib(anna, new { statementNumber = 1, answer = true }).Accepted, "Im Vorlauf wurde ein Urteil angenommen.");
            Assert.IsFalse(Gib(anna, new { statementNumber = 1, answer = false }).Accepted, "Im Vorlauf wurde ein Urteil angenommen.");
        }

        /// <summary>Das Urteil kommt nach der Frist an, die Runde ist aber noch nicht geschlossen - die Uhr entscheidet.</summary>
        [TestMethod]
        public void AVerdictAfterTheDeadlineDoesNotCountAndTheWatchClosesTheRound()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf + Dauer - TimeSpan.FromMilliseconds(5));

            Assert.IsNull(spiel.CloseIfDue(), "Die Runde schliesst vor der Frist.");
            Assert.IsTrue(Gib(anna, new { statementNumber = 1, answer = true }).Accepted, "Kurz vor der Frist zaehlt das Urteil nicht.");

            uhr.Vor(TimeSpan.FromMilliseconds(10));
            Assert.AreEqual(MinigamePhase.Running, spiel.Phase, "Die Runde ist schon zu - die Probe prueft dann den Rahmen, nicht die Uhr.");
            Assert.IsFalse(Gib(bert, new { statementNumber = 1, answer = true }).Accepted, "Ein Urteil nach der Frist wurde angenommen.");

            var zu = spiel.CloseIfDue();

            Assert.IsNotNull(zu, "Nach der Frist schliesst die Runde nicht.");
            Assert.IsTrue(zu.Finished);
            Assert.AreEqual(1, ((BlitzrundeStand)zu.Submissions[anna.Id]).Beantwortet);
            Assert.IsFalse(zu.Submissions.ContainsKey(bert.Id), "Das spaete Urteil steht in der Wertung.");
        }

        [TestMethod]
        public void AnOldStatementAForeignKindOrABrokenPayloadIsRejected()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            Assert.IsFalse(Gib(anna, new { statementNumber = 1 }).Accepted, "Ein Urteil ohne Antwort wurde angenommen.");
            Assert.IsFalse(Gib(anna, 42).Accepted, "Eine Zahl als Nutzlast wurde angenommen.");
            Assert.IsFalse(Gib(anna, new { statementNumber = 1, answer = true }, MinigameKind.MentalArithmetic).Accepted, "Eine fremde Art wurde angenommen.");
            Assert.IsFalse(Gib(anna, new { statementNumber = 2, answer = true }).Accepted, "Eine kuenftige Aussage wurde angenommen.");

            Assert.IsTrue(Gib(anna, new { statementNumber = 1, answer = true }).Accepted);
            uhr.Vor(TimeSpan.FromSeconds(1));
            Assert.IsFalse(Gib(anna, new { statementNumber = 1, answer = false }).Accepted, "Dieselbe Aussage zaehlte zweimal.");
            Assert.AreEqual(1, Art(anna).OwnAnswered, "Das doppelte Urteil hat den Stand veraendert.");
            Assert.AreEqual(2, Art(anna).StatementNumber);
        }

        /// <summary>Mehr als drei Urteile je Sekunde schafft kein Mensch - das vierte wird verworfen, nach der Sekunde geht es weiter.</summary>
        [TestMethod]
        public void MoreVerdictsPerSecondThanTheLimitAreRejected()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            for (var i = 0; i < BlitzrundeEvaluator.MaxAntwortenJeSekunde; i++)
            {
                uhr.Vor(TimeSpan.FromMilliseconds(10));
                Assert.IsTrue(Gib(anna, new { statementNumber = Art(anna).StatementNumber, answer = true }).Accepted, $"Urteil {i + 1} wurde verworfen.");
            }

            var naechste = Art(anna).StatementNumber;
            Assert.IsFalse(Gib(anna, new { statementNumber = naechste, answer = true }).Accepted, "Die Grenze greift nicht.");
            Assert.AreEqual(naechste, Art(anna).StatementNumber, "Ein verworfenes Urteil hat die Aussage weitergeschaltet.");

            uhr.Vor(TimeSpan.FromSeconds(1));
            Assert.IsTrue(Gib(anna, new { statementNumber = naechste, answer = true }).Accepted, "Nach der Sekunde geht es nicht weiter.");
        }

        /// <summary>Die Grenze gilt je Spieler: wer schnell urteilt, bremst niemand anderen aus.</summary>
        [TestMethod]
        public void TheLimitCountsPerPlayer()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            foreach (var spieler in new[] { anna, bert })
            {
                for (var i = 0; i < BlitzrundeEvaluator.MaxAntwortenJeSekunde; i++)
                {
                    uhr.Vor(TimeSpan.FromMilliseconds(10));
                    Assert.IsTrue(Gib(spieler, new { statementNumber = Art(spieler).StatementNumber, answer = true }).Accepted,
                        $"Urteil {i + 1} von {spieler.Designation} wurde verworfen - die Grenze zaehlt alle zusammen.");
                }
            }
        }

        /// <summary>Wer alle beurteilt hat, ist gesperrt; haben es alle, schliesst die Runde mit richtig und falsch je Spieler.</summary>
        [TestMethod]
        public void TheRoundClosesWhenEveryoneHasJudgedEverything()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            for (var i = 0; i < Aussagen.Length; i++)
                Assert.IsTrue(Urteile(anna, richtig: i < 4).Accepted);

            Assert.IsTrue(spiel.IsLockedFor(anna), "Wer alle beurteilt hat, darf weiter abgeben.");
            Assert.IsTrue(((MinigameClientInfo)spiel.InfoFor(anna)).OwnSubmitted);
            Assert.AreEqual(0, Art(anna).StatementNumber, "Nach der letzten steht noch eine Aussage da.");
            Assert.IsNull(Art(anna).OwnCorrect, "Vor dem Ende der Runde steht das Ergebnis da - Bert urteilt noch.");

            MinigameRoundClosed? zu = null;
            for (var i = 0; i < Aussagen.Length; i++)
                zu = Urteile(bert, richtig: i % 2 == 0).Closed ?? zu;

            Assert.IsNotNull(zu, "Haben alle alles beurteilt, schliesst die Runde nicht.");
            Assert.AreEqual(new BlitzrundeStand(4, 2), zu.Submissions[anna.Id]);
            Assert.AreEqual(new BlitzrundeStand(3, 3), zu.Submissions[bert.Id]);
            Assert.AreEqual(4, Art(anna).OwnCorrect, "Nach der Runde fehlt das Ergebnis.");
            Assert.AreEqual(2, Art(anna).OwnWrong);
        }

        [TestMethod]
        public void AReconnectingPhoneGetsItsStatementBack()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);
            Urteile(anna, richtig: true);
            Urteile(anna, richtig: false);

            anna.ConnectionState = PlayerConnection.Disconnected;
            Assert.IsNull(spiel.CloseIfComplete(), "Nach der Trennung schliesst die Runde, obwohl Bert noch urteilt.");
            anna.ConnectionState = PlayerConnection.Connected;

            var info = (MinigameClientInfo)spiel.InfoFor(anna);
            var art = (LightningRoundClientInfo)info.KindInfo!;

            Assert.IsFalse(info.OwnSubmitted);
            Assert.AreEqual(3, art.StatementNumber);
            Assert.AreEqual(2, art.OwnAnswered);
            Assert.IsTrue(art.RemainingMs is > 0 and < 60_000, "Die Restzeit fehlt oder kommt nicht von der Serveruhr: " + art.RemainingMs);
        }

        /// <summary>
        /// Kein Telefon und nicht der Blick von aussen sieht eine kuenftige Aussage oder ein Ergebnis, solange die
        /// Runde laeuft - die laufende Aussage aber schon (sonst misst die Probe nichts).
        /// </summary>
        [TestMethod]
        public void NoFutureStatementAndNoResultIsInAnyStateWhileTheRoundRuns()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);
            Urteile(anna, richtig: true);

            var annas = Zustandsinhalt.Von(spiel.InfoFor(anna));
            Assert.IsTrue(annas.EnthaeltText(Art(anna).StatementText!), "Die laufende Aussage steht nicht im Zustand - die Probe misst sonst nichts.");

            foreach (var p in new[] { anna, bert, null })
            {
                var inhalt = Zustandsinhalt.Von(spiel.InfoFor(p));
                var eigene = p == null ? null : Art(p).StatementText;
                var fremd = Aussagen.Select(a => a.Text).Where(t => t != eigene).ToList();

                foreach (var text in fremd)
                    Assert.IsFalse(inhalt.EnthaeltText(text), $"Im Zustand von {p?.Designation ?? "aussen"} steht eine andere Aussage: {text}");

                Assert.IsNull(p == null ? null : Art(p).OwnCorrect, $"{p?.Designation}: das Ergebnis steht im Zustand.");
            }

            Assert.IsNull(Art(null).StatementText, "Der Blick von aussen sieht eine Aussage.");
        }

        /// <summary>
        /// Die Gegenprobe ueber alle Felder: zwei Inhalte, die sich nur in der Wahrheit der Aussagen unterscheiden,
        /// geben Anna, Bert und dem Blick von aussen in jeder Phase der laufenden Runde genau denselben Zustand. Erst
        /// nach dem Ende duerfen sie sich unterscheiden - und tun es, sonst misst die Probe nichts.
        /// </summary>
        [TestMethod]
        public void TwoContentsThatDifferOnlyInTheTruthGiveTheSameStatesWhileTheRoundRuns()
        {
            var umgedreht = Aussagen.Select(a => a with { Wahr = !a.Wahr }).ToList();
            var zweite = Neu();

            Starte(spiel);
            Starte(zweite, umgedreht);
            Gleich("im Vorlauf");
            uhr.Vor(Vorlauf);

            for (var i = 0; i < Aussagen.Length; i++)
            {
                uhr.Vor(TimeSpan.FromMilliseconds(400));
                var nummer = Art(anna).StatementNumber;
                var a = Gib(spiel, anna, new { statementNumber = nummer, answer = true });
                var b = Gib(zweite, anna, new { statementNumber = nummer, answer = true });

                Assert.AreEqual(a.Accepted, b.Accepted);
                Assert.AreEqual(a.Value, b.Value, "An den Spielleiter geht etwas, das die Wahrheit traegt.");
                Gleich($"nach Annas Urteil {i + 1}");

                if (i < 2)
                {
                    Assert.AreEqual(Gib(spiel, bert, new { statementNumber = i + 1, answer = false }).Accepted,
                        Gib(zweite, bert, new { statementNumber = i + 1, answer = false }).Accepted);
                    Gleich($"nach Berts Urteil {i + 1}");
                }
            }

            uhr.Vor(Dauer);
            Assert.IsNotNull(spiel.CloseIfDue());
            Assert.IsNotNull(zweite.CloseIfDue());
            Assert.AreNotEqual(Json(spiel, anna), Json(zweite, anna), "Nach der Runde unterscheiden sich die Ergebnisse nicht - die Probe misst nichts.");

            void Gleich(string wann)
            {
                foreach (var p in new[] { anna, bert, null })
                    Assert.AreEqual(Json(spiel, p), Json(zweite, p), $"{wann}: der Zustand von {p?.Designation ?? "aussen"} verraet die Wahrheit.");
            }
        }

        /// <summary>Der ganze Zustand, ohne den Durchgang - der ist je Instanz zufaellig und traegt nichts.</summary>
        private static string Json(MinigameState zustand, Player? p)
        {
            var info = (MinigameClientInfo)zustand.InfoFor(p);
            info.Durchgang = 0;
            return JsonSerializer.Serialize(info, info.GetType());
        }
    }
}
