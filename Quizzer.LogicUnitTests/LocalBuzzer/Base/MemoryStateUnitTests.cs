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
    /// Das Memory am Server (M35 C5) ohne Netz: jedes Umdrehen wird an der Serveruhr geprueft, zwei offene
    /// ohne Paar schliessen beim naechsten, wer fertig ist, ist gesperrt - und die Belegung steht in keinem
    /// Zustand, in keiner Phase. Die Belegung ist bekannt: Paar k liegt auf den Karten 2k und 2k+1.
    /// </summary>
    [TestClass]
    public class MemoryStateUnitTests
    {
        private const int Paare = 6;
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan Dauer = TimeSpan.FromSeconds(60);

        /// <summary>Kein Tausch: Motive in Grundordnung, Karten [0, 0, 1, 1, …] (<see cref="MemoryBelegung.Mische"/>).</summary>
        private static int Grundordnung(int von, int bis) => bis == MemoryBelegung.Motive.Count ? von : bis - 1;

        private Player anna = null!;
        private Player bert = null!;
        private StellbareUhr uhr = null!;
        private MinigameState spiel = null!;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            uhr = new StellbareUhr();
            spiel = Neu(Grundordnung);
        }

        private MinigameState Neu(Func<int, int, int> zufall)
        {
            var game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { anna, bert })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            return new MinigameState(new GameAccessor { GetGame = () => game }) { Zufall = zufall, Uhr = uhr };
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private static void Starte(MinigameState zustand)
        {
            zustand.Configure(new MinigameSetup(MinigameKind.Memory, "Memory", "Regel", 1, DurationSeconds: (int)Dauer.TotalSeconds));
            zustand.Reset();
            Assert.IsTrue(zustand.StartNextRound(), "Die Runde liess sich nicht starten.");
        }

        private void Starte() => Starte(spiel);

        private static MinigameSubmitResult Gib(MinigameState zustand, Player p, object nutzlast, MinigameKind art = MinigameKind.Memory)
            => zustand.Submit(p, new MinigameAction
            {
                Kind = art,
                Durchgang = zustand.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        /// <summary>Dreht eine Karte um; mit Abstand, damit die Grenze nicht greift.</summary>
        private MinigameSubmitResult Drehe(Player p, int karte, MinigameState? zustand = null)
        {
            uhr.Vor(TimeSpan.FromMilliseconds(250));
            return Gib(zustand ?? spiel, p, new { card = karte });
        }

        private MemoryClientInfo Art(Player? p, MinigameState? zustand = null)
            => (MemoryClientInfo)((MinigameClientInfo)(zustand ?? spiel).InfoFor(p)).KindInfo!;

        private static List<int> Indizes(List<MemoryCardInfo> karten) => karten.Select(k => k.Index).ToList();

        private void FindeAlle(Player p)
        {
            for (var k = 0; k < Paare; k++)
            {
                Assert.IsTrue(Drehe(p, 2 * k).Accepted);
                Assert.IsTrue(Drehe(p, 2 * k + 1).Accepted);
            }
        }

        [TestMethod]
        public void APairIsFoundAndAMismatchStaysOpenUntilTheNextFlip()
        {
            Starte();
            uhr.Vor(Vorlauf);

            Assert.AreEqual(new MemoryZug(0, false, new MemoryStand(0, null)), Drehe(anna, 0).Value);
            CollectionAssert.AreEqual(new[] { 0 }, Indizes(Art(anna).OpenCards));
            Assert.AreEqual(MemoryBelegung.Motive[0].Symbol, Art(anna).OpenCards[0].Symbol, "Die offene Karte zeigt nicht ihr Motiv.");

            Assert.AreEqual(new MemoryZug(1, true, new MemoryStand(1, null)), Drehe(anna, 1).Value);
            Assert.AreEqual(0, Art(anna).OpenCards.Count, "Ein gefundenes Paar liegt noch offen.");
            CollectionAssert.AreEqual(new[] { 0, 1 }, Indizes(Art(anna).FoundCards));

            Drehe(anna, 2);
            Assert.IsFalse(((MemoryZug)Drehe(anna, 4).Value!).Paar);
            CollectionAssert.AreEqual(new[] { 2, 4 }, Indizes(Art(anna).OpenCards), "Zwei ohne Paar sind nicht beide zu sehen.");

            Drehe(anna, 6);
            CollectionAssert.AreEqual(new[] { 6 }, Indizes(Art(anna).OpenCards), "Das naechste Umdrehen schliesst die zwei ohne Paar nicht.");

            Assert.AreEqual(0, Art(bert).OpenCards.Count + Art(bert).FoundCards.Count, "Bert sieht Annas Feld.");
            Assert.IsNull(Drehe(anna, 7).Closed, "Die Runde schliesst, bevor einer fertig ist.");
        }

        [TestMethod]
        public void AFoundCardTheOpenCardAgainABrokenOrForeignFlipIsRejected()
        {
            Starte();
            uhr.Vor(Vorlauf);
            Drehe(anna, 0);
            Drehe(anna, 1);
            Drehe(anna, 2);

            Assert.IsFalse(Drehe(anna, 0).Accepted, "Eine gefundene Karte liess sich umdrehen.");
            Assert.IsFalse(Drehe(anna, 2).Accepted, "Die offene Karte liess sich noch einmal umdrehen - ein Paar mit sich selbst.");
            Assert.IsFalse(Drehe(anna, Paare * 2).Accepted, "Eine Karte ausserhalb des Felds wurde angenommen.");
            Assert.IsFalse(Drehe(anna, -1).Accepted, "Eine negative Karte wurde angenommen.");
            Assert.IsFalse(Gib(spiel, anna, new { }).Accepted, "Eine leere Nutzlast wurde angenommen.");
            Assert.IsFalse(Gib(spiel, anna, new { card = 3 }, MinigameKind.MentalArithmetic).Accepted, "Eine fremde Art wurde angenommen.");

            CollectionAssert.AreEqual(new[] { 2 }, Indizes(Art(anna).OpenCards), "Eine verworfene Abgabe hat das Feld veraendert.");
            Assert.AreEqual(1, Art(anna).OwnPairs);
        }

        [TestMethod]
        public void DuringTheCountdownNothingIsAccepted()
        {
            Starte();
            uhr.Vor(TimeSpan.FromSeconds(1));

            Assert.IsFalse(Gib(spiel, anna, new { card = 0 }).Accepted, "Im Vorlauf wurde eine Karte umgedreht.");
        }

        /// <summary>Mehr als fuenf Karten in einer Sekunde dreht kein Mensch - die sechste wird verworfen.</summary>
        [TestMethod]
        public void MoreFlipsPerSecondThanTheLimitAreRejected()
        {
            Starte();
            uhr.Vor(Vorlauf);

            for (var i = 0; i < MemoryEvaluator.MaxUmdrehenJeSekunde; i++)
            {
                uhr.Vor(TimeSpan.FromMilliseconds(10));
                Assert.IsTrue(Gib(spiel, anna, new { card = 2 * i }).Accepted, $"Umdrehen {i + 1} wurde verworfen.");
            }

            Assert.IsFalse(Gib(spiel, anna, new { card = 11 }).Accepted, "Die Grenze greift nicht.");

            uhr.Vor(TimeSpan.FromSeconds(1));
            Assert.IsTrue(Gib(spiel, anna, new { card = 11 }).Accepted, "Nach der Sekunde geht es nicht weiter.");
        }

        [TestMethod]
        public void WhoFinishesIsLockedAtTheServerClockAndTheLastOneClosesTheRound()
        {
            Starte();
            uhr.Vor(Vorlauf);

            FindeAlle(anna);

            var annaFertig = TimeSpan.FromMilliseconds(250 * 2 * Paare);
            Assert.AreEqual((int)annaFertig.TotalMilliseconds, Art(anna).OwnFinishedMs, "Die Zeit kommt nicht von der Serveruhr.");
            Assert.IsTrue(spiel.IsLockedFor(anna), "Wer fertig ist, darf weiter umdrehen.");
            Assert.IsTrue(((MinigameClientInfo)spiel.InfoFor(anna)).OwnSubmitted);
            Assert.AreEqual(MinigamePhase.Running, spiel.Phase, "Die Runde schliesst, obwohl Bert noch spielt.");

            for (var k = 0; k < Paare - 1; k++)
            {
                Drehe(bert, 2 * k);
                Drehe(bert, 2 * k + 1);
            }

            Drehe(bert, 2 * Paare - 2);
            var letzte = Drehe(bert, 2 * Paare - 1);

            Assert.IsNotNull(letzte.Closed, "Alle sind fertig, die Runde laeuft weiter.");
            Assert.AreEqual(new MemoryStand(Paare, annaFertig), letzte.Closed.Submissions[anna.Id]);
            Assert.IsTrue(Art(anna).IsWinner, "Die Schnellere hat nicht gewonnen.");
            CollectionAssert.AreEqual(new[] { "Anna" }, Art(bert).WinnerNames);
        }

        /// <summary>Das Umdrehen kommt nach der Frist an, die Runde ist aber noch nicht geschlossen - die Uhr entscheidet.</summary>
        [TestMethod]
        public void AFlipAfterTheDeadlineDoesNotCountAndTheRoundClosesThen()
        {
            Starte();
            uhr.Vor(Vorlauf);
            Drehe(anna, 0);
            Drehe(anna, 1);
            Drehe(bert, 2);

            uhr.Vor(Dauer);
            Assert.AreEqual(MinigamePhase.Running, spiel.Phase, "Die Runde ist schon zu - die Probe prueft dann den Rahmen, nicht die Uhr.");
            Assert.IsFalse(Gib(spiel, bert, new { card = 3 }).Accepted, "Ein Umdrehen nach der Frist wurde angenommen.");

            var zu = spiel.CloseIfDue();

            Assert.IsNotNull(zu, "Nach der Frist schliesst die Runde nicht.");
            Assert.AreEqual(new MemoryStand(0, null), zu.Submissions[bert.Id]);
            Assert.IsTrue(Art(anna).IsWinner, "Ohne Fertige gewinnt nicht, wer die meisten Paare hat.");
            Assert.AreEqual(0, Art(anna).OpenCards.Count, "Nach der Runde liegen Karten offen.");
        }

        /// <summary>Ein Telefon, das wiederkommt, bekommt sein Feld zurueck - offene Karten und Paare.</summary>
        [TestMethod]
        public void AReconnectingPhoneGetsItsBoardBack()
        {
            Starte();
            uhr.Vor(Vorlauf);
            Drehe(anna, 4);
            Drehe(anna, 5);
            Drehe(anna, 8);

            anna.ConnectionState = PlayerConnection.Disconnected;
            Assert.IsNull(spiel.CloseIfComplete(), "Nach der Trennung schliesst die Runde.");
            anna.ConnectionState = PlayerConnection.Connected;

            var art = Art(anna);
            CollectionAssert.AreEqual(new[] { 4, 5 }, Indizes(art.FoundCards));
            CollectionAssert.AreEqual(new[] { 8 }, Indizes(art.OpenCards));
            Assert.AreEqual(1, art.OwnPairs);
            Assert.IsTrue(art.RemainingMs is > 0 and < 60_000, "Die Restzeit fehlt: " + art.RemainingMs);
        }

        /// <summary>
        /// Kein Zustand - kein Telefon, nicht der Blick von aussen - traegt ein Motiv, das dieses Telefon nicht
        /// offen hat oder gefunden hat, in keiner Phase, auch nach dem Ende und nach einem Abbruch nicht.
        /// </summary>
        [TestMethod]
        public void NoStateCarriesAMotifItHasNotSeen()
        {
            Pruefe("vor dem Start", []);
            Starte();
            Pruefe("im Vorlauf", []);
            uhr.Vor(Vorlauf);

            // Je Karte die Paare, die Anna danach sehen darf: gefunden oder gerade offen - 2 und 4 schliessen mit der 6.
            foreach (var (karte, sichtbar) in new (int, List<int>)[] { (0, [0]), (1, [0]), (2, [0, 1]), (4, [0, 1, 2]), (6, [0, 3]) })
            {
                Assert.IsTrue(Drehe(anna, karte).Accepted);
                Pruefe($"nach Karte {karte}", sichtbar);
            }

            uhr.Vor(Dauer);
            Assert.IsNotNull(spiel.CloseIfDue());
            Pruefe("nach der Runde", [0]);

            Starte();
            uhr.Vor(Vorlauf);
            Drehe(anna, 10);
            Assert.IsNotNull(spiel.Abort());
            Pruefe("nach dem Abbruch", []);

            void Pruefe(string wann, List<int> annaSieht)
            {
                foreach (var (p, sieht) in new (Player?, List<int>)[] { (anna, annaSieht), (bert, []), (null, []) })
                {
                    var inhalt = Zustandsinhalt.Von(spiel.InfoFor(p));

                    for (var paar = 0; paar < Paare; paar++)
                    {
                        var motiv = MemoryBelegung.Motive[paar];
                        var darf = sieht.Contains(paar);

                        Assert.AreEqual(darf, inhalt.EnthaeltText(motiv.Symbol), $"{wann}: Motiv {motiv.Name} {(darf ? "fehlt" : "steht")} im Zustand von {p?.Designation ?? "aussen"}.");
                        Assert.AreEqual(darf, inhalt.EnthaeltText(motiv.Name), $"{wann}: Name {motiv.Name} {(darf ? "fehlt" : "steht")} im Zustand von {p?.Designation ?? "aussen"}.");
                    }
                }
            }
        }

        /// <summary>
        /// Die Gegenprobe ueber alle Felder: zwei Belegungen, die sich nur auf Karten unterscheiden, die Anna nie
        /// umdreht, geben ihr in jeder Phase genau denselben Zustand - auch kein Index verraet, was wo liegt.
        /// </summary>
        [TestMethod]
        public void TwoLayoutsThatDifferOnlyWhereAnnaNeverLookedGiveHerTheSameState()
        {
            var letzte = 2 * Paare - 1;

            // Wie die Grundordnung, nur der erste Kartenwurf tauscht die letzte Karte mit der ersten.
            int Getauscht(int von, int bis) => bis == 2 * Paare && von == 0 ? 0 : Grundordnung(von, bis);

            var a = MemoryBelegung.Mische(Paare, Grundordnung);
            var b = MemoryBelegung.Mische(Paare, Getauscht);
            CollectionAssert.AreEqual(Enumerable.Range(0, letzte + 1).Where(i => a[i] != b[i]).ToList(), new[] { 0, letzte },
                "Die Belegungen unterscheiden sich nicht genau auf der ersten und letzten Karte - die Probe misst sonst etwas anderes.");

            var zweite = Neu(Getauscht);
            Starte(spiel);
            Starte(zweite);
            Gleich("im Vorlauf");
            uhr.Vor(Vorlauf);

            foreach (var karte in new[] { 2, 3, 4, 6, 1, letzte - 1, 5 })
            {
                uhr.Vor(TimeSpan.FromMilliseconds(250));
                Assert.AreEqual(Gib(spiel, anna, new { card = karte }).Accepted, Gib(zweite, anna, new { card = karte }).Accepted);
                Gleich($"nach Karte {karte}");
            }

            uhr.Vor(Dauer);
            Assert.IsNotNull(spiel.CloseIfDue());
            Assert.IsNotNull(zweite.CloseIfDue());
            Gleich("nach der Runde");

            void Gleich(string wann)
            {
                var hier = JsonSerializer.Serialize(Art(anna, spiel));
                var dort = JsonSerializer.Serialize(Art(anna, zweite));
                Assert.AreEqual(hier, dort, $"{wann}: Annas Zustand verraet etwas ueber Karten, die sie nie gesehen hat.");
            }
        }
    }
}
