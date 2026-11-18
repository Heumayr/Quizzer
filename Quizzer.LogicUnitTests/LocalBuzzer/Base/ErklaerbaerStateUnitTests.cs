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
    /// Der Erklaerbaer am Server (M35 C6) ohne Netz: den Begriff sieht nur der Erklaerer; der erste richtige Rater
    /// schliesst die Runde, ein zweiter zaehlt nicht; der Erklaerer kann nicht raten; die Grenze gilt je Spieler; eine
    /// Wiederholung zieht einen neuen Begriff.
    /// </summary>
    [TestClass]
    public class ErklaerbaerStateUnitTests
    {
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan Dauer = TimeSpan.FromSeconds(60);

        private static readonly string[] Begriffe = ["Käse", "Leuchtturm", "Zahnbürste"];

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

        /// <summary>Anna hat das Feld aufgedeckt - sie erklaert, wenn nichts anderes gesagt ist.</summary>
        private void Starte(MinigameState zustand, IEnumerable<string>? begriffe = null, Guid? ausloeser = null, bool ohneAusloeser = false)
        {
            zustand.Configure(new MinigameSetup(MinigameKind.ExplainIt, "Erklärbär", "Regel", 2,
                DurationSeconds: (int)Dauer.TotalSeconds, TriggerPlayerId: ohneAusloeser ? null : ausloeser ?? anna.Id,
                Content: (begriffe ?? Begriffe).Select(b => new MinispielZeile(b, false)).ToList()));
            zustand.Reset();
            Assert.IsTrue(zustand.StartNextRound(), "Die Runde liess sich nicht starten.");
        }

        private static MinigameSubmitResult Gib(MinigameState zustand, Player p, string eingabe)
            => zustand.Submit(p, new MinigameAction
            {
                Kind = MinigameKind.ExplainIt,
                Durchgang = zustand.Durchgang,
                Payload = JsonSerializer.SerializeToElement(new { guess = eingabe }),
            });

        /// <summary>Ein Versuch mit Abstand, damit die Grenze nicht greift.</summary>
        private MinigameSubmitResult Rate(Player p, string eingabe, MinigameState? zustand = null)
        {
            uhr.Vor(TimeSpan.FromSeconds(2));
            return Gib(zustand ?? spiel, p, eingabe);
        }

        private static ExplainItClientInfo Art(MinigameState zustand, Player? p)
            => (ExplainItClientInfo)((MinigameClientInfo)zustand.InfoFor(p)).KindInfo!;

        private ExplainItClientInfo Art(Player? p) => Art(spiel, p);

        [TestMethod]
        public void OnlyTheExplainerSeesTheTermAndOnlyWhileTheRoundRuns()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            Assert.AreEqual("Käse", Art(anna).Term);
            Assert.IsTrue(Art(anna).IsExplainer);
            Assert.AreEqual("Anna", Art(bert).ExplainerName);
            Assert.IsNull(Art(bert).Term);
            Assert.IsNull(Art(null).Term);

            Assert.IsNotNull(Rate(bert, "Kaese").Closed, "Der Treffer schliesst die Runde nicht - ae gilt nicht wie ä.");
            Assert.IsNull(Art(anna).Term, "Nach der Runde steht der Begriff noch als Erklaerer-Begriff da.");
        }

        /// <summary>Der erste richtige Rater schliesst die Runde fuer alle; eine zweite richtige Eingabe zaehlt nicht.</summary>
        [TestMethod]
        public void TheFirstRightGuessClosesTheRoundAndASecondDoesNotCount()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            var treffer = Rate(bert, "  KAESE ");
            var zweiter = Rate(carl, "Käse");

            Assert.IsTrue(treffer.Accepted);
            Assert.IsNotNull(treffer.Closed, "Der Treffer schliesst die Runde nicht.");
            Assert.AreEqual(new ErklaerbaerVersuch(true, anna.Id), treffer.Closed.Submissions[bert.Id]);
            Assert.AreEqual(1, treffer.Closed.Submissions.Count, "Neben dem Rater steht noch jemand in der Runde.");
            Assert.IsFalse(zweiter.Accepted, "Nach dem Treffer zaehlt eine zweite richtige Eingabe.");

            Assert.AreEqual("Käse", Art(carl).Solution, "Nach der Runde fehlt die Aufloesung.");
            Assert.AreEqual("Bert", Art(null).SolverName);
            Assert.IsTrue(Art(bert).IsSolver);
            Assert.IsFalse(Art(carl).IsSolver);
        }

        /// <summary>Der Erklaerer kennt den Begriff - sein Versuch zaehlt nie, auch wenn sein Telefon ihn schickt.</summary>
        [TestMethod]
        public void TheExplainerCannotGuess()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            var versuch = Rate(anna, "Käse");

            Assert.IsFalse(versuch.Accepted, "Der Server nimmt den Versuch des Erklaerers an.");
            Assert.IsNull(versuch.Closed);
            Assert.AreEqual(MinigamePhase.Running, spiel.Phase, "Der Erklaerer hat die Runde geschlossen.");
        }

        [TestMethod]
        public void AWrongGuessOnlyThisPhoneHearsOf()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            var daneben = Rate(bert, "Wurst");

            Assert.IsTrue(daneben.Accepted, "Ein Fehlversuch wird verworfen - das Telefon erfuehre nie, dass er danebenlag.");
            Assert.IsFalse(daneben.ForAll);
            Assert.AreEqual(new ErklaerbaerVersuch(false, anna.Id), daneben.Value);
            Assert.IsTrue(Art(bert).LastWrong);
            Assert.IsFalse(Art(carl).LastWrong, "Carl sieht Berts Fehlversuch.");
        }

        /// <summary>Die Grenze gilt je Spieler: wer viel raet, bremst niemand anderen aus - und sie greift.</summary>
        [TestMethod]
        public void TheLimitCountsPerPlayer()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);

            foreach (var spieler in new[] { bert, carl })
            {
                for (var i = 0; i < ErklaerbaerEvaluator.MaxVersucheJeFenster; i++)
                {
                    uhr.Vor(TimeSpan.FromMilliseconds(10));
                    Assert.IsTrue(Gib(spiel, spieler, $"Versuch {i}").Accepted,
                        $"Versuch {i + 1} von {spieler.Designation} wurde verworfen - die Grenze zaehlt alle zusammen.");
                }
            }

            Assert.IsFalse(Gib(spiel, bert, "Käse").Accepted, "Ueber der Grenze nimmt der Server noch an.");
        }

        /// <summary>Ein alter Durchgang zaehlt nicht; eine Wiederholung zieht den naechsten Begriff, der alte ist erklaert.</summary>
        [TestMethod]
        public void ARepeatDrawsTheNextTermAndAnOldRoundDoesNotCount()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);
            var alt = spiel.Durchgang;

            Assert.IsTrue(spiel.RepeatRound());
            uhr.Vor(Vorlauf);
            Assert.AreEqual("Leuchtturm", Art(anna).Term, "Die Wiederholung spielt denselben Begriff.");

            var veraltet = spiel.Submit(bert, new MinigameAction
            {
                Kind = MinigameKind.ExplainIt,
                Durchgang = alt,
                Payload = JsonSerializer.SerializeToElement(new { guess = "Leuchtturm" }),
            });
            Assert.IsFalse(veraltet.Accepted, "Eine Abgabe aus dem alten Durchgang zaehlt.");
            Assert.IsNull(Rate(bert, "Käse").Closed, "Der alte Begriff loest die neue Runde.");
        }

        /// <summary>Ohne Ausloeser erklaert niemand, bis der Spielleiter einen festlegt (E1); ein Gegenueber gibt es nicht.</summary>
        [TestMethod]
        public void WithoutATriggerTheMasterPicksTheExplainer()
        {
            Starte(spiel, ohneAusloeser: true);
            uhr.Vor(Vorlauf);
            var regeln = (ExplainItRules)spiel.Rules!;

            Assert.IsNull(regeln.ChooserId);
            Assert.IsFalse(Rate(bert, "Käse").Accepted, "Ohne Erklaerer wird geraten.");
            Assert.IsFalse(spiel.SetPair(anna.Id, bert.Id), "Der Erklaerbaer nimmt ein Gegenueber an.");
            Assert.IsFalse(spiel.SetPair(Guid.NewGuid(), null), "Ein Fremder wird Erklaerer.");
            Assert.IsTrue(spiel.SetPair(carl.Id, null));
            Assert.AreEqual(carl.Id, regeln.ChooserId);
        }

        /// <summary>Wechselt der Erklaerer mitten in der Runde, kommt ein neuer Begriff - den alten kennt der bisherige.</summary>
        [TestMethod]
        public void ANewExplainerMidRoundGetsANewTerm()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);
            Assert.AreEqual("Käse", Art(anna).Term);

            Assert.IsTrue(spiel.SetPair(bert.Id, null));
            uhr.Vor(Vorlauf);

            Assert.AreEqual("Leuchtturm", Art(bert).Term, "Der neue Erklaerer bekommt den alten Begriff.");
            Assert.IsNull(Art(anna).Term, "Die bisherige Erklaererin sieht noch einen Begriff.");
            Assert.IsNull(Rate(anna, "Käse").Closed, "Der alte Begriff loest die Runde noch.");
        }

        [TestMethod]
        public void TheDeadlineClosesTheRoundWithoutASolver()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf + Dauer);

            var zu = spiel.CloseIfDue();

            Assert.IsNotNull(zu, "Die Frist schliesst die Runde nicht.");
            Assert.AreEqual(0, zu.Submissions.Count);
            Assert.AreEqual("Käse", Art(null).Solution);
            Assert.IsNull(Art(null).SolverName);
        }

        /// <summary>M37 P6, P2-1 (dieselbe Form wie V25): ein Abbruch nach dem Treffer lässt stehen, wer erraten hat.</summary>
        [TestMethod]
        public void AnAbortAfterTheHitKeepsWhoSolvedIt()
        {
            Starte(spiel);
            uhr.Vor(Vorlauf);
            Assert.IsNotNull(Rate(bert, "Käse").Closed, "Der Treffer schliesst die Runde nicht.");

            Assert.IsNotNull(spiel.Abort(), "Nach dem Treffer laesst sich nicht abbrechen.");

            Assert.AreEqual("Bert", Art(null).SolverName, "Nach dem Abbruch steht da, niemand habe es erraten.");
            Assert.IsTrue(Art(bert).IsSolver, "Nach dem Abbruch weiss Bert nicht mehr, dass er es erraten hat.");
        }

        /// <summary>
        /// Kein Begriff in irgendeinem Zustand ausser dem des Erklaerers - in keiner Schreibweise, fuer kein Telefon und
        /// nicht von aussen. Nach der Runde steht er bei allen, sonst misst die Probe nichts.
        /// </summary>
        [TestMethod]
        public void TheTermIsInNoStateButTheExplainers()
        {
            Starte(spiel);
            Pruefe("im Vorlauf");
            uhr.Vor(Vorlauf);
            Pruefe("zu Beginn");
            Rate(bert, "Kase");
            Rate(carl, "Kuchen");
            Pruefe("nach Fehlversuchen");

            Assert.IsTrue(Zustandsinhalt.Von(spiel.InfoFor(anna)).EnthaeltText("Käse"), "Der Begriff steht nicht beim Erklaerer - die Probe misst sonst nichts.");

            Rate(bert, "Käse");
            Assert.IsTrue(Zustandsinhalt.Von(spiel.InfoFor(carl)).EnthaeltText("Käse"), "Nach der Runde fehlt die Aufloesung - die Probe misst sonst nichts.");

            void Pruefe(string wann)
            {
                foreach (var p in new[] { bert, carl, null })
                {
                    var inhalt = Zustandsinhalt.Von(spiel.InfoFor(p));

                    foreach (var form in new[] { "Käse", "KÄSE", "käse", BuchstabensalatBegriffe.Normalisiere("Käse") })
                        Assert.IsFalse(inhalt.Texte.Any(t => t.Contains(form, StringComparison.OrdinalIgnoreCase)),
                            $"{wann}: der Begriff steht im Zustand von {p?.Designation ?? "aussen"}.");
                }
            }
        }

        /// <summary>
        /// Die Gegenprobe ueber alle Felder: zwei Inhalte, die sich nur im Begriff unterscheiden - verschieden lang, damit
        /// auch eine Zahl ihn verriete -, geben Bert, Carl und dem Blick von aussen denselben Zustand, auch nach
        /// Fehlversuchen. Beim Erklaerer und nach der Runde unterscheiden sie sich, und dann sieht die Probe es auch.
        /// </summary>
        [TestMethod]
        public void TwoContentsThatDifferOnlyInTheTermGiveTheSameStatesToEveryoneButTheExplainer()
        {
            var erste = Neu();
            var zweite = Neu();
            Starte(erste, ["Käse", "Brot"]);
            Starte(zweite, ["Schmetterling", "Brot"]);

            Gleich("im Vorlauf");
            uhr.Vor(Vorlauf);
            Gleich("zu Beginn");
            Assert.AreNotEqual(Json(erste, anna), Json(zweite, anna), "Beim Erklaerer unterscheiden sich die Zustaende nicht - die Probe misst nichts.");

            foreach (var (p, eingabe) in new[] { (bert, "Wurst"), (carl, "Tisch"), (bert, "Kuchen") })
            {
                uhr.Vor(TimeSpan.FromSeconds(2));
                var x = Gib(erste, p, eingabe);
                var y = Gib(zweite, p, eingabe);
                Assert.AreEqual(x.Accepted, y.Accepted);
                Assert.AreEqual(x.Value, y.Value, "An den Spielleiter geht etwas, das den Begriff traegt.");
                Gleich($"nach „{eingabe}“ von {p.Designation}");
            }

            uhr.Vor(TimeSpan.FromSeconds(2));
            Assert.IsNotNull(Gib(erste, bert, "Käse").Closed);
            Assert.IsNotNull(Gib(zweite, bert, "Schmetterling").Closed);
            Assert.AreNotEqual(Json(erste, null), Json(zweite, null), "Nach der Runde unterscheiden sich die Zustaende nicht - die Probe misst nichts.");

            void Gleich(string wann)
            {
                foreach (var p in new[] { bert, carl, null })
                    Assert.AreEqual(Json(erste, p), Json(zweite, p), $"{wann}: der Zustand von {p?.Designation ?? "aussen"} verraet den Begriff.");
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
