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
    /// Der Buchstabensalat am Server (M35 C5) ohne Netz: die erste richtige Eingabe loest den Begriff fuer alle,
    /// eine zweite zaehlt nicht; verglichen wird ohne Gross/klein und mit gleichgesetzten Umlauten; und ein offener
    /// Begriff steht in keinem Zustand.
    /// </summary>
    [TestClass]
    public class BuchstabensalatStateUnitTests
    {
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan Dauer = TimeSpan.FromSeconds(90);

        private static readonly string[] Begriffe = ["Käse", "Schmetterling", "Fußball"];

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

            spiel = Neu(new Random(42).Next);
        }

        private MinigameState Neu(Func<int, int, int> zufall) => new(new GameAccessor { GetGame = () => game }) { Zufall = zufall, Uhr = uhr };

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private static void Starte(MinigameState zustand, IEnumerable<string>? begriffe = null)
        {
            zustand.Configure(new MinigameSetup(MinigameKind.WordScramble, "Buchstabensalat", "Regel", 1,
                DurationSeconds: (int)Dauer.TotalSeconds, Content: (begriffe ?? Begriffe).Select(b => new MinispielZeile(b, false)).ToList()));
            zustand.Reset();
            Assert.IsTrue(zustand.StartNextRound(), "Die Runde liess sich nicht starten.");
        }

        private static MinigameSubmitResult Gib(MinigameState zustand, Player p, object nutzlast, MinigameKind art = MinigameKind.WordScramble)
            => zustand.Submit(p, new MinigameAction
            {
                Kind = art,
                Durchgang = zustand.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private MinigameSubmitResult Gib(Player p, object nutzlast, MinigameKind art = MinigameKind.WordScramble) => Gib(spiel, p, nutzlast, art);

        /// <summary>Ein Versuch am offenen Begriff; mit Abstand, damit die Grenze nicht greift.</summary>
        private MinigameSubmitResult Rate(Player p, string eingabe, MinigameState? zustand = null)
        {
            zustand ??= spiel;
            uhr.Vor(TimeSpan.FromSeconds(2));
            return Gib(zustand, p, new { termNumber = Art(zustand, p).TermNumber, guess = eingabe });
        }

        private static WordScrambleClientInfo Art(MinigameState zustand, Player? p)
            => (WordScrambleClientInfo)((MinigameClientInfo)zustand.InfoFor(p)).KindInfo!;

        private WordScrambleClientInfo Art(Player? p) => Art(spiel, p);

        [TestMethod]
        public void AWrongGuessDoesNotCountAndOnlyThisPhoneHearsOfIt()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            var daneben = Rate(anna, "Kase");

            Assert.IsTrue(daneben.Accepted, "Ein Fehlversuch wird verworfen - das Telefon erfuehre nie, dass er danebenlag.");
            Assert.IsFalse(daneben.ForAll, "Ein Fehlversuch geht an alle Telefone.");
            Assert.AreEqual(new BuchstabensalatVersuch(1, null, new(0)), daneben.Value);
            Assert.AreEqual(1, Art(anna).TermNumber, "Ein Fehlversuch hat den Begriff weitergeschaltet.");
            Assert.IsTrue(Art(anna).LastWrong);
            Assert.IsFalse(Art(bert).LastWrong, "Bert sieht Annas Fehlversuch.");
            Assert.AreEqual(0, Art(anna).OwnSolved);
        }

        /// <summary>Die erste richtige Eingabe loest den Begriff, fuer alle kommt der naechste; die zweite zaehlt nicht.</summary>
        [TestMethod]
        public void TheFirstRightGuessSolvesTheTermForEveryoneAndASecondDoesNotCount()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);
            var erstesWirrwarr = Art(bert).Scramble;

            var anna1 = Gib(anna, new { termNumber = 1, guess = "Käse" });
            var bert1 = Gib(bert, new { termNumber = 1, guess = "Käse" });

            Assert.IsTrue(anna1.Accepted && anna1.ForAll, "Die Loesung geht nicht an alle Telefone.");
            Assert.AreEqual(new BuchstabensalatVersuch(1, "Käse", new(1)), anna1.Value);
            Assert.IsFalse(bert1.Accepted, "Eine zweite richtige Eingabe fuer denselben Begriff zaehlte.");

            foreach (var p in new[] { anna, bert })
            {
                Assert.AreEqual(2, Art(p).TermNumber, $"{p.Designation} sieht nicht den naechsten Begriff.");
                Assert.AreEqual("Käse", Art(p).LastSolved);
                Assert.AreEqual("Anna", Art(p).LastSolver);
            }

            Assert.AreNotEqual(erstesWirrwarr, Art(bert).Scramble);
            Assert.AreEqual(1, Art(anna).OwnSolved);
            Assert.AreEqual(0, Art(bert).OwnSolved, "Die zu spaete Loesung hat gezaehlt.");
        }

        [TestMethod]
        [DataRow("  kaese ")]
        [DataRow("KÄSE")]
        [DataRow("käse")]
        [DataRow("KAESE")]
        public void TheComparisonIgnoresCaseUmlautsAndOuterSpaces(string eingabe)
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            Assert.AreEqual("Käse", (Rate(anna, eingabe).Value as BuchstabensalatVersuch)?.Loesung, $"„{eingabe}“ loest „Käse“ nicht.");
        }

        [TestMethod]
        public void DuringTheCountdownNothingIsAccepted()
        {
            Starte(spiel);
            uhr.Vor(TimeSpan.FromSeconds(1));

            Assert.IsFalse(Gib(anna, new { termNumber = 1, guess = "Käse" }).Accepted, "Im Vorlauf wurde eine Loesung angenommen.");
            Assert.AreEqual(1, Art(anna).TermNumber);
        }

        /// <summary>Die Eingabe kommt nach der Frist an, die Runde ist aber noch nicht geschlossen - die Uhr entscheidet.</summary>
        [TestMethod]
        public void AGuessAfterTheDeadlineDoesNotCountAndTheWatchClosesTheRound()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf + Dauer - TimeSpan.FromMilliseconds(5));

            Assert.IsNull(spiel.CloseIfDue(), "Die Runde schliesst vor der Frist.");
            Assert.IsTrue(Gib(anna, new { termNumber = 1, guess = "Käse" }).Accepted, "Kurz vor der Frist zaehlt die Loesung nicht.");

            uhr.Vor(TimeSpan.FromMilliseconds(10));
            Assert.AreEqual(MinigamePhase.Running, spiel.Phase, "Die Runde ist schon zu - die Probe prueft dann den Rahmen, nicht die Uhr.");
            Assert.IsFalse(Gib(bert, new { termNumber = 2, guess = "Schmetterling" }).Accepted, "Eine Loesung nach der Frist wurde angenommen.");

            var zu = spiel.CloseIfDue();

            Assert.IsNotNull(zu, "Nach der Frist schliesst die Runde nicht.");
            Assert.AreEqual(new BuchstabensalatStand(1), zu.Submissions[anna.Id]);
            Assert.IsFalse(zu.Submissions.ContainsKey(bert.Id), "Die spaete Eingabe steht in der Wertung.");
            Assert.IsTrue(Art(anna).IsWinner);
            CollectionAssert.AreEqual(new[] { "Anna" }, Art(bert).WinnerNames);
        }

        [TestMethod]
        public void AnOldTermAForeignKindOrABrokenPayloadIsRejected()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            Assert.IsFalse(Gib(anna, new { termNumber = 1 }).Accepted, "Eine Abgabe ohne Eingabe wurde angenommen.");
            Assert.IsFalse(Gib(anna, new { termNumber = 1, guess = "   " }).Accepted, "Eine leere Eingabe wurde angenommen.");
            Assert.IsFalse(Gib(anna, 42).Accepted, "Eine Zahl als Nutzlast wurde angenommen.");
            Assert.IsFalse(Gib(anna, new { termNumber = 1, guess = "Käse" }, MinigameKind.MentalArithmetic).Accepted, "Eine fremde Art wurde angenommen.");
            Assert.IsFalse(Gib(anna, new { termNumber = 2, guess = "Schmetterling" }).Accepted, "Ein kuenftiger Begriff wurde angenommen.");

            Assert.IsTrue(Gib(anna, new { termNumber = 1, guess = "Käse" }).Accepted);
            uhr.Vor(TimeSpan.FromSeconds(2));
            Assert.IsFalse(Gib(anna, new { termNumber = 1, guess = "Käse" }).Accepted, "Derselbe Begriff zaehlte zweimal.");
            Assert.AreEqual(1, Art(anna).OwnSolved, "Die doppelte Loesung hat den Stand veraendert.");
        }

        /// <summary>Raten ist hier der Missbrauch: mehr als drei Versuche in fuenf Sekunden werden verworfen, auch ein richtiger.</summary>
        [TestMethod]
        public void MoreGuessesThanTheLimitAreRejectedEvenARightOne()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            for (var i = 0; i < BuchstabensalatEvaluator.MaxVersucheJeFenster; i++)
            {
                uhr.Vor(TimeSpan.FromMilliseconds(100));
                Assert.IsTrue(Gib(anna, new { termNumber = 1, guess = $"Versuch {i}" }).Accepted, $"Versuch {i + 1} wurde verworfen.");
            }

            uhr.Vor(TimeSpan.FromMilliseconds(100));
            Assert.IsFalse(Gib(anna, new { termNumber = 1, guess = "Käse" }).Accepted, "Die Grenze greift nicht.");
            Assert.AreEqual(1, Art(anna).TermNumber, "Ein verworfener Versuch hat den Begriff geloest.");

            uhr.Vor(TimeSpan.FromSeconds(BuchstabensalatEvaluator.FensterSekunden));
            Assert.IsTrue(Gib(anna, new { termNumber = 1, guess = "Käse" }).ForAll, "Nach dem Fenster geht es nicht weiter.");
        }

        /// <summary>Die Grenze gilt je Spieler: wer viel raet, bremst niemand anderen aus.</summary>
        [TestMethod]
        public void TheLimitCountsPerPlayer()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            foreach (var spieler in new[] { anna, bert })
            {
                for (var i = 0; i < BuchstabensalatEvaluator.MaxVersucheJeFenster; i++)
                {
                    uhr.Vor(TimeSpan.FromMilliseconds(10));
                    Assert.IsTrue(Gib(spieler, new { termNumber = 1, guess = $"Versuch {i}" }).Accepted,
                        $"Versuch {i + 1} von {spieler.Designation} wurde verworfen - die Grenze zaehlt alle zusammen.");
                }
            }
        }

        [TestMethod]
        public void WhenEveryTermIsSolvedTheRoundCloses()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            Assert.IsNull(Rate(anna, "kaese").Closed);
            Assert.IsNull(Rate(bert, "SCHMETTERLING").Closed);
            var letzte = Rate(anna, "Fussball");

            Assert.IsNotNull(letzte.Closed, "Sind alle Begriffe geloest, schliesst die Runde nicht.");
            Assert.IsTrue(letzte.Closed.Finished);
            Assert.AreEqual(new BuchstabensalatStand(2), letzte.Closed.Submissions[anna.Id]);
            Assert.AreEqual(new BuchstabensalatStand(1), letzte.Closed.Submissions[bert.Id]);
            Assert.IsTrue(Art(anna).IsWinner);
            Assert.AreEqual(0, Art(anna).TermNumber);
            Assert.IsNull(Art(anna).Scramble);
        }

        [TestMethod]
        public void AReconnectingPhoneGetsTheOpenTermBack()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);
            Rate(anna, "Käse");
            Rate(bert, "falsch");

            bert.ConnectionState = PlayerConnection.Disconnected;
            Assert.IsNull(spiel.CloseIfComplete(), "Nach der Trennung schliesst die Runde.");
            bert.ConnectionState = PlayerConnection.Connected;

            var art = Art(bert);
            Assert.AreEqual(2, art.TermNumber);
            Assert.IsTrue(art.LastWrong);
            Assert.IsTrue(art.RemainingMs is > 0 and < 90_000, "Die Restzeit fehlt oder kommt nicht von der Serveruhr: " + art.RemainingMs);
        }

        /// <summary>
        /// Kein offener Begriff in irgendeinem Zustand - in keiner Schreibweise, fuer kein Telefon und nicht von
        /// aussen; kein kuenftiges Wirrwarr. Das laufende Wirrwarr aber schon, sonst misst die Probe nichts.
        /// </summary>
        [TestMethod]
        public void NoOpenTermIsInAnyState()
        {
            Starte(spiel);
            Pruefe("im Vorlauf", 0);
            uhr.Vor(Vorlauf);
            Rate(bert, "Kase");
            Pruefe("nach einem Fehlversuch", 0);
            Rate(anna, "Käse");
            Pruefe("nach der ersten Loesung", 1);

            Assert.IsTrue(Zustandsinhalt.Von(spiel.InfoFor(bert)).EnthaeltText(Art(bert).Scramble!), "Das Wirrwarr steht nicht im Zustand - die Probe misst sonst nichts.");

            void Pruefe(string wann, int geloest)
            {
                foreach (var p in new[] { anna, bert, null })
                {
                    var inhalt = Zustandsinhalt.Von(spiel.InfoFor(p));

                    foreach (var begriff in Begriffe.Skip(geloest))
                    {
                        foreach (var form in new[] { begriff, begriff.ToUpperInvariant(), begriff.ToLowerInvariant(), BuchstabensalatBegriffe.Normalisiere(begriff) })
                            Assert.IsFalse(inhalt.Texte.Any(t => t.Contains(form, StringComparison.OrdinalIgnoreCase)),
                                $"{wann}: der offene Begriff „{begriff}“ steht im Zustand von {p?.Designation ?? "aussen"}.");
                    }
                }
            }
        }

        /// <summary>
        /// Die Gegenprobe ueber alle Felder: zwei Inhalte aus Anagrammen (Lampe/Palme, Regal/Lager) mit demselben
        /// Wirrwarr geben Anna, Bert und dem Blick von aussen genau denselben Zustand, solange der Begriff offen ist -
        /// auch nach Fehlversuchen. Erst die Loesung macht sie verschieden, und dann sieht die Probe es auch.
        /// </summary>
        [TestMethod]
        public void TwoAnagramContentsWithTheSameScrambleGiveTheSameStatesWhileTheTermIsOpen()
        {
            string[] wirrwarr = ["EMPAL", "GELRA", "DNUH"];
            string[] a = ["Lampe", "Regal", "Hund"];
            string[] b = ["Palme", "Lager", "Hund"];

            var erste = Neu(ZufallFuer(a, wirrwarr));
            var zweite = Neu(ZufallFuer(b, wirrwarr));
            Starte(erste, a);
            Starte(zweite, b);
            Assert.AreEqual("EMPAL", Art(erste, anna).Scramble, "Das erzwungene Wirrwarr greift nicht - die Probe misst sonst etwas anderes.");

            Gleich("im Vorlauf");
            uhr.Vor(Vorlauf);

            foreach (var (p, eingabe) in new[] { (anna, "Ampel"), (bert, "Lempa"), (anna, "Mapel") })
            {
                var x = Rate(p, eingabe, erste);
                var y = Gib(zweite, p, new { termNumber = 1, guess = eingabe });
                Assert.AreEqual(x.Accepted, y.Accepted);
                Assert.AreEqual(x.Value, y.Value, "An den Spielleiter geht etwas, das den offenen Begriff traegt.");
                Gleich($"nach „{eingabe}“ von {p.Designation}");
            }

            Rate(anna, "Lampe", erste);
            Gib(zweite, anna, new { termNumber = 1, guess = "Palme" });
            Assert.AreNotEqual(Json(erste, anna), Json(zweite, anna), "Nach der Loesung unterscheiden sich die Zustaende nicht - die Probe misst nichts.");

            // Der zweite Begriff ist offen; was die erste Loesung nennt, darf sich unterscheiden.
            Rate(bert, "Regen", erste);
            Gib(zweite, bert, new { termNumber = 2, guess = "Regen" });
            Gleich("am zweiten Begriff", ohneGeloest: true);

            void Gleich(string wann, bool ohneGeloest = false)
            {
                foreach (var p in new[] { anna, bert, null })
                    Assert.AreEqual(Json(erste, p, ohneGeloest), Json(zweite, p, ohneGeloest), $"{wann}: der Zustand von {p?.Designation ?? "aussen"} verraet den offenen Begriff.");
            }
        }

        /// <summary>Der ganze Zustand ohne den Durchgang (je Instanz zufaellig); auf Wunsch ohne den geloesten Begriff.</summary>
        private static string Json(MinigameState zustand, Player? p, bool ohneGeloest = false)
        {
            var info = (MinigameClientInfo)zustand.InfoFor(p);
            info.Durchgang = 0;

            if (ohneGeloest && info.KindInfo is WordScrambleClientInfo art)
                art.LastSolved = null;

            return JsonSerializer.Serialize(info, info.GetType());
        }

        /// <summary>
        /// Eine Zufallsquelle, mit der das Verwuerfeln genau <paramref name="ziel"/> ergibt - dieselbe Mischung
        /// (Fisher-Yates von hinten) wie <see cref="BuchstabensalatBegriffe.Verwuerfle"/>, rueckwaerts gelesen.
        /// </summary>
        private static Func<int, int, int> ZufallFuer(string[] begriffe, string[] ziel)
        {
            var folge = new Queue<int>();

            for (var k = 0; k < begriffe.Length; k++)
            {
                var jetzt = begriffe[k].ToUpperInvariant().Where(char.IsLetter).ToArray();
                var soll = ziel[k].Where(char.IsLetter).ToArray();

                for (var i = jetzt.Length - 1; i > 0; i--)
                {
                    var j = Array.FindIndex(jetzt, 0, i + 1, c => c == soll[i]);
                    (jetzt[i], jetzt[j]) = (jetzt[j], jetzt[i]);
                    folge.Enqueue(j);
                }
            }

            return (_, _) => folge.Dequeue();
        }
    }
}
