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
    /// Der Bluff am Server (M35 C6) ohne Netz: Phase 1 nimmt nur Bluffs, Phase 2 nur Wahlen; der eigene Bluff fehlt in der
    /// Liste und wird abgewiesen; was der echten gleicht, ist gestrichen; gleiche Bluffs sind eine Antwort. Und nirgends
    /// steht, wer welchen Bluff schrieb - und vor dem Ende nicht, welche Antwort echt ist.
    /// </summary>
    [TestClass]
    public class BluffStateUnitTests
    {
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan Dauer = TimeSpan.FromSeconds(60);

        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private Game game = null!;
        private StellbareUhr uhr = null!;
        private MinigameState spiel = null!;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");
            uhr = new StellbareUhr();

            game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { anna, bert, carl })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            spiel = Neu();
        }

        private MinigameState Neu() => new(new GameAccessor { GetGame = () => game }) { Zufall = new Random(42).Next, Uhr = uhr };

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private void Starte(MinigameState zustand, params string[] echt)
        {
            zustand.Configure(new MinigameSetup(MinigameKind.Bluff, "Bluff", "Regel", 2, DurationSeconds: (int)Dauer.TotalSeconds,
                Content: (echt.Length > 0 ? echt : ["Rom"]).Select(t => new MinispielZeile(t, false)).ToList()));
            zustand.Reset();
            Assert.IsTrue(zustand.StartNextRound(), "Phase 1 liess sich nicht starten.");
            uhr.Vor(Vorlauf);
        }

        private static MinigameSubmitResult Gib(MinigameState zustand, Player p, object nutzlast, int? durchgang = null)
            => zustand.Submit(p, new MinigameAction
            {
                Kind = MinigameKind.Bluff,
                Durchgang = durchgang ?? zustand.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private static MinigameSubmitResult Bluffe(MinigameState zustand, Player p, string text) => Gib(zustand, p, new { bluff = text });

        private static MinigameSubmitResult Waehle(MinigameState zustand, Player p, int nummer) => Gib(zustand, p, new { choice = nummer });

        /// <summary>Phase 1 endet an der Frist (Carl schreibt nichts), dann startet die Wahl - in allen zugleich.</summary>
        private void ZurWahl(params MinigameState[] zustaende)
        {
            uhr.Vor(Dauer);
            foreach (var zustand in zustaende)
                Assert.IsNotNull(zustand.CloseIfDue(), "Phase 1 schliesst an der Frist nicht.");
            foreach (var zustand in zustaende)
                Assert.IsTrue(zustand.StartNextRound(), "Phase 2 liess sich nicht starten.");
            uhr.Vor(Vorlauf);
        }

        private static BluffClientInfo Art(MinigameState zustand, Player? p)
            => (BluffClientInfo)((MinigameClientInfo)zustand.InfoFor(p)).KindInfo!;

        private static int Nummer(MinigameState zustand, string text) => Art(zustand, null).Options.Single(o => o.Text == text).Number;

        [TestMethod]
        public void PhaseOneTakesOnlyBluffsAndPhaseTwoOnlyChoices()
        {
            Starte(spiel);
            Assert.IsFalse(Waehle(spiel, anna, 1).Accepted, "Phase 1 nimmt eine Wahl an.");
            Assert.IsFalse(Gib(spiel, anna, new { bluff = "Paris", choice = 1 }).Accepted, "Phase 1 nimmt eine Abgabe mit Wahl an.");
            Assert.AreEqual(new BluffAbgabe(1), Bluffe(spiel, anna, "Paris").Value);
            Assert.IsFalse(Bluffe(spiel, anna, "Wien").Accepted, "Ein zweiter Bluff zaehlt.");
            Assert.AreEqual("Paris", Art(spiel, anna).OwnBluff);
            var phase1 = spiel.Durchgang;

            ZurWahl(spiel);
            var rom = Nummer(spiel, "Rom");

            Assert.IsFalse(Bluffe(spiel, bert, "Wien").Accepted, "Phase 2 nimmt einen Bluff an.");
            Assert.IsFalse(Gib(spiel, bert, new { bluff = "Wien", choice = rom }).Accepted, "Phase 2 nimmt eine Abgabe mit Bluff an.");
            Assert.IsFalse(Gib(spiel, bert, new { choice = rom }, phase1).Accepted, "Eine Wahl aus dem Durchgang der Phase 1 zaehlt.");
            Assert.IsFalse(Waehle(spiel, bert, 99).Accepted, "Eine unbekannte Nummer zaehlt.");
            Assert.AreEqual(new BluffAbgabe(2), Waehle(spiel, bert, rom).Value);
            Assert.IsFalse(Waehle(spiel, bert, Nummer(spiel, "Paris")).Accepted, "Eine zweite Wahl zaehlt.");
        }

        /// <summary>Den eigenen Bluff zeigt das Telefon nicht - und schickt es die Nummer doch, weist der Server sie ab.</summary>
        [TestMethod]
        public void TheOwnBluffIsHiddenAndRejectedByTheServer()
        {
            Starte(spiel);
            Bluffe(spiel, anna, "Paris");
            Bluffe(spiel, bert, "Wien");
            ZurWahl(spiel);

            CollectionAssert.AreEquivalent(new[] { "Rom", "Wien" }, Art(spiel, anna).Options.Select(o => o.Text).ToArray(), "Anna sieht ihren eigenen Bluff.");
            CollectionAssert.AreEquivalent(new[] { "Paris", "Rom", "Wien" }, Art(spiel, carl).Options.Select(o => o.Text).ToArray(), "Wer nichts schrieb, sieht nicht alle.");

            Assert.IsFalse(Waehle(spiel, anna, Nummer(spiel, "Paris")).Accepted, "Der Server nimmt die Wahl des eigenen Bluffs an.");
            Assert.IsTrue(Waehle(spiel, carl, Nummer(spiel, "Paris")).Accepted, "Wer nichts schrieb, darf nicht waehlen.");
        }

        /// <summary>Was der echten gleicht, ist von selbst gestrichen und bleibt es; der Spielleiter streicht nur zwischen den Phasen.</summary>
        [TestMethod]
        public void StruckBluffsAreNotOfferedAndTheMasterStrikesOnlyBetweenThePhases()
        {
            Starte(spiel, "Rom", "Roma");
            var regeln = (BluffRules)spiel.Rules!;
            Bluffe(spiel, anna, "Paris");
            Bluffe(spiel, bert, "  ROMA ");
            Assert.IsNotNull(Bluffe(spiel, carl, "Mailand").Closed, "Haben alle geschrieben, schliesst Phase 1 nicht.");

            var roma = regeln.Eingaenge.Single(e => e.Text == "ROMA");
            Assert.IsTrue(roma.VonSelbst && roma.Gestrichen, "Ein Bluff, der einer Schreibweise der echten gleicht, steht zur Wahl.");
            Assert.IsFalse(regeln.Streiche(roma.Schluessel, false), "Der Spielleiter holt einen Bluff zurueck, der der echten gleicht.");
            Assert.IsTrue(regeln.Streiche("mailand", true));

            Assert.IsTrue(spiel.StartNextRound());
            uhr.Vor(Vorlauf);
            Assert.IsFalse(regeln.Streiche("paris", true), "Waehrend der Wahl laesst sich noch streichen.");
            CollectionAssert.AreEquivalent(new[] { "Paris", "Rom" }, Art(spiel, null).Options.Select(o => o.Text).ToArray());
            Assert.IsFalse(Art(spiel, bert).OwnStruck || Art(spiel, carl).OwnStruck, "Vor dem Ende erfaehrt ein Telefon, dass sein Bluff gestrichen ist.");

            Waehle(spiel, anna, Nummer(spiel, "Rom"));
            Waehle(spiel, bert, Nummer(spiel, "Paris"));
            Assert.IsNotNull(Waehle(spiel, carl, Nummer(spiel, "Paris")).Closed, "Haben alle gewaehlt, schliesst Phase 2 nicht.");

            Assert.IsTrue(Art(spiel, carl).OwnStruck && Art(spiel, bert).OwnStruck);
            Assert.AreEqual(2, Art(spiel, anna).FooledCount);
            Assert.IsTrue(Art(spiel, anna).FoundReal);
            Assert.AreEqual("Rom", Art(spiel, null).RealAnswer);
        }

        /// <summary>Zwei gleiche Bluffs sind eine Antwort; beide Urheber sehen sie nicht und beide zaehlen den Reinfall.</summary>
        [TestMethod]
        public void EqualBluffsAreOneOptionAndCountForBothAuthors()
        {
            Starte(spiel);
            Bluffe(spiel, anna, "Paris");
            Bluffe(spiel, bert, " paris");
            ZurWahl(spiel);

            Assert.AreEqual(1, Art(spiel, carl).Options.Count(o => BuchstabensalatBegriffe.Gleich(o.Text, "Paris")), "Gleiche Bluffs stehen zweimal da.");
            Assert.IsFalse(Art(spiel, bert).Options.Any(o => BuchstabensalatBegriffe.Gleich(o.Text, "Paris")), "Bert sieht den gemeinsamen Bluff.");

            Waehle(spiel, carl, Nummer(spiel, Art(spiel, carl).Options.Single(o => o.Text != "Rom").Text));
            uhr.Vor(Dauer);
            Assert.IsNotNull(spiel.CloseIfDue());

            Assert.AreEqual(1, Art(spiel, anna).FooledCount);
            Assert.AreEqual(1, Art(spiel, bert).FooledCount);
            var regeln = (BluffRules)spiel.Rules!;
            Assert.AreEqual(2, regeln.Auswertung!.Optionen.Single(o => !o.Echt).Urheber.Count);
        }

        /// <summary>In Phase 1 sieht kein Telefon einen fremden Bluff - nur den eigenen; die Probe sieht ihn dort, sonst misst sie nichts.</summary>
        [TestMethod]
        public void NoPhoneSeesAnotherBluffInPhaseOne()
        {
            Starte(spiel);
            Bluffe(spiel, anna, "Paris");

            foreach (var p in new[] { bert, carl, null })
                Assert.IsFalse(Zustandsinhalt.Von(spiel.InfoFor(p)).EnthaeltText("Paris"), $"Annas Bluff steht im Zustand von {p?.Designation ?? "aussen"}.");
            Assert.IsTrue(Zustandsinhalt.Von(spiel.InfoFor(anna)).EnthaeltText("Paris"), "Annas eigener Bluff fehlt - die Probe misst sonst nichts.");
            Assert.IsNull(Art(spiel, null).RealAnswer);
        }

        /// <summary>
        /// Gegenprobe zur echten Antwort: dieselben drei Antworten, aber eine andere ist echt. Bert, Carl und der Blick von
        /// aussen bekommen waehrend der Wahl genau denselben Zustand; erst nach der Wahl unterscheiden sie sich.
        /// </summary>
        [TestMethod]
        public void WhichAnswerIsRealIsInNoStateWhileChoosing()
        {
            var erste = Neu();
            var zweite = Neu();
            Starte(erste, "Rom");
            Starte(zweite, "Paris");
            Bluffe(erste, anna, "Paris");
            Bluffe(zweite, anna, "Rom");
            Bluffe(erste, bert, "Wien");
            Bluffe(zweite, bert, "Wien");
            ZurWahl(erste, zweite);

            Gleich("zu Beginn der Wahl");
            Waehle(erste, carl, Nummer(erste, "Wien"));
            Waehle(zweite, carl, Nummer(zweite, "Wien"));
            Gleich("nach Carls Wahl");

            uhr.Vor(Dauer);
            Assert.IsNotNull(erste.CloseIfDue());
            Assert.IsNotNull(zweite.CloseIfDue());
            Assert.AreNotEqual(Json(erste, null), Json(zweite, null), "Nach der Wahl unterscheiden sich die Zustaende nicht - die Probe misst nichts.");

            void Gleich(string wann)
            {
                foreach (var p in new[] { bert, carl, null })
                    Assert.AreEqual(Json(erste, p), Json(zweite, p), $"{wann}: der Zustand von {p?.Designation ?? "aussen"} verraet, welche Antwort echt ist.");
            }
        }

        /// <summary>
        /// Gegenprobe zu den Urhebern: Anna und Bert tauschen ihre Bluffs. Carl und der Blick von aussen bekommen genau
        /// denselben Zustand - waehrend der Wahl und auch danach; Annas Zustand unterscheidet sich danach, sonst misst die Probe nichts.
        /// </summary>
        [TestMethod]
        public void WhoWroteWhichBluffIsInNoStateEvenAfterTheEnd()
        {
            var erste = Neu();
            var zweite = Neu();
            Starte(erste);
            Starte(zweite);
            Bluffe(erste, anna, "Paris");
            Bluffe(zweite, anna, "Wien");
            Bluffe(erste, bert, "Wien");
            Bluffe(zweite, bert, "Paris");
            ZurWahl(erste, zweite);

            Gleich("zu Beginn der Wahl");
            Waehle(erste, carl, Nummer(erste, "Paris"));
            Waehle(zweite, carl, Nummer(zweite, "Paris"));
            Gleich("nach Carls Wahl");

            uhr.Vor(Dauer);
            Assert.IsNotNull(erste.CloseIfDue());
            Assert.IsNotNull(zweite.CloseIfDue());
            Gleich("nach der Wahl");
            Assert.AreNotEqual(Json(erste, anna), Json(zweite, anna), "Annas Zustand unterscheidet sich nach der Wahl nicht - die Probe misst nichts.");

            void Gleich(string wann)
            {
                foreach (var p in new[] { carl, null })
                    Assert.AreEqual(Json(erste, p), Json(zweite, p), $"{wann}: der Zustand von {p?.Designation ?? "aussen"} verraet, wer welchen Bluff schrieb.");
            }
        }

        /// <summary>Der ganze Zustand ohne den Durchgang (je Instanz zufaellig).</summary>
        private static string Json(MinigameState zustand, Player? p)
        {
            var info = (MinigameClientInfo)zustand.InfoFor(p);
            info.Durchgang = 0;
            return JsonSerializer.Serialize(info, info.GetType());
        }
    }
}
