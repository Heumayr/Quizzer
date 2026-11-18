using LocalBuzzer.Service.Base;
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
    /// Die Staffel am Server (M37 T11) - die Missbrauchsfälle des Entwurfs: eine Abgabe, wer nicht dran ist (falsches Mitglied,
    /// falsches Team, Zuschauer, beim Start getrennt), dieselbe Abgabe doppelt, eine Abgabe nach dem Weitergeben, fremde Kennungen
    /// in der Nutzlast, eine Lösung am Telefon vor der Auflösung, die Grenze von fünf Antworten in zehn Sekunden und von 80 Zeichen.
    /// Dazu: die Eingriffe der Spielleitung über den Rahmen, die Runde schließt nach dem letzten Teil.
    /// </summary>
    [TestClass]
    public class StaffelStateUnitTests
    {
        private static readonly string[] Planeten = ["Merkur", "Venus", "Erde", "Mars", "Jupiter", "Saturn"];

        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private Player fritz = null!;
        private Player dora = null!;
        private Player emil = null!;
        private Game game = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;
        private StellbareUhr uhr = null!;
        private MinigameState spiel = null!;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");
            fritz = Spieler("Fritz");
            dora = Spieler("Dora");
            emil = Spieler("Emil");
            uhr = new StellbareUhr();
            game = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Rot", ColorIndex = 5 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Blau", ColorIndex = 4 };
            game.GameTeams.AddRange([rot, blau]);

            // Anna und Bert sind Rot, Carl und Fritz Blau, Dora ohne Team; Emil ist Blau, aber beim Start getrennt.
            foreach (var (p, team) in new[] { (anna, rot), (bert, rot), (carl, blau), (fritz, blau), (dora, (GameTeam?)null), (emil, blau) })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p, GameTeamId = team?.Id });

            emil.ConnectionState = PlayerConnection.Disconnected;
            var aufstellung = TeamRoster.From(game);
            spiel = new MinigameState(new GameAccessor { GetGame = () => game }, () => aufstellung) { Uhr = uhr };
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        /// <summary>Rot beginnt (Anna hat das Feld aufgedeckt): Anna, dann Bert; Blau mit Carl und Fritz.</summary>
        private void Starte(Player? ausloeser = null, params string[] teile)
        {
            var inhalt = (teile.Length > 0 ? teile : Planeten).Select(t => new MinispielZeile(t, false)).ToList();
            spiel.Configure(new MinigameSetup(MinigameKind.Relay, "Staffel", "Regel", 1, TriggerPlayerId: (ausloeser ?? anna).Id, Content: inhalt));
            spiel.Reset();
            Assert.IsTrue(spiel.StartNextRound(), "Die Staffel liess sich nicht starten.");
        }

        private MinigameSubmitResult Gib(Player p, object nutzlast)
            => spiel.Submit(p, new MinigameAction
            {
                Kind = MinigameKind.Relay,
                Durchgang = spiel.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private MinigameSubmitResult Antworte(Player p, string antwort, int? zug = null) => Gib(p, new { turn = zug ?? Stand.Zug, answer = antwort });

        private StaffelStand Stand => spiel.Lenke(r => ((RelayRules)r!).Stand);

        private RelayClientInfo Info(Player? p) => (RelayClientInfo)((MinigameClientInfo)spiel.InfoFor(p)).KindInfo!;

        [TestMethod]
        public void OnlyTheRunnerAnswersAndOnlyOnHisTurn()
        {
            Starte();
            Assert.AreEqual(anna.Id, Stand.Laeufer, "Rot beginnt nicht mit Anna.");

            Assert.IsFalse(Antworte(bert, "Merkur").Accepted, "Bert ist nicht dran und antwortet für Rot.");
            Assert.IsFalse(Antworte(carl, "Merkur").Accepted, "Blau antwortet, während Rot läuft.");
            Assert.IsFalse(Antworte(dora, "Merkur").Accepted, "Dora ohne Team antwortet.");
            Assert.IsFalse(Antworte(anna, "Merkur", zug: Stand.Zug + 1).Accepted, "Ein fremder Zug zählt.");
            Assert.IsFalse(Antworte(anna, "   ").Accepted, "Eine leere Antwort zählt als Fehler.");

            var abgabe = Antworte(anna, "merkur");
            Assert.IsTrue(abgabe.Accepted && abgabe.ForAll, "Annas Antwort geht nicht an alle Telefone.");
            Assert.AreEqual(new StaffelAntwort(1, rot.Id, "merkur", true), abgabe.Value);
            Assert.AreEqual((bert.Id, 1), (Stand.Laeufer, Stand.Teil), "Nach Anna läuft nicht Bert den zweiten Teil.");

            Assert.IsFalse(Antworte(anna, "Venus", zug: 1).Accepted, "Anna antwortet noch einmal auf ihren alten Zug.");
        }

        /// <summary>Carl läuft für Blau allein - derselbe Läufer bleibt dran, nur der Zug schützt vor der doppelten Abgabe.</summary>
        [TestMethod]
        public void TheSamePartSentTwiceCountsOnce()
        {
            fritz.ConnectionState = PlayerConnection.Disconnected;
            Starte(carl);
            var zug = Stand.Zug;

            Assert.IsTrue(Antworte(carl, "Merkur", zug).Accepted);
            Assert.IsFalse(Antworte(carl, "Merkur", zug).Accepted, "Dieselbe Abgabe zählt zweimal - als Antwort auf den nächsten Teil.");
            Assert.AreEqual((blau.Id, 1), (Stand.TeamAmZug, Stand.Teil), "Die doppelte Abgabe hat den Teil weitergegeben.");
            Assert.IsTrue(Antworte(carl, "Venus").Accepted, "Carl kann seinen nächsten Teil nicht abgeben.");
        }

        [TestMethod]
        public void AnAnswerAfterThePassIsRejected()
        {
            Starte();
            var alterZug = Stand.Zug;

            Assert.IsTrue(spiel.Lenke(r => ((RelayRules)r!).GibWeiter()), "Die Spielleitung kann nicht weitergeben.");
            Assert.AreEqual(blau.Id, Stand.TeamAmZug);

            Assert.IsFalse(Antworte(anna, "Merkur", alterZug).Accepted, "Anna antwortet nach dem Weitergeben.");
            Assert.IsFalse(Antworte(carl, "Merkur", alterZug).Accepted, "Carl antwortet mit dem Zug von vor dem Weitergeben.");
            Assert.IsTrue(Antworte(carl, "Merkur").Accepted, "Carl kann nach dem Weitergeben nicht antworten.");
            Assert.AreEqual(blau.Id, Stand.Geloest[0]);
        }

        [TestMethod]
        public void AForeignPlayerOrTeamInThePayloadDoesNotCount()
        {
            Starte();

            var abgabe = Gib(anna, new { turn = Stand.Zug, answer = "Merkur", playerId = carl.Id, teamId = blau.Id });
            Assert.IsTrue(abgabe.Accepted);
            Assert.AreEqual(rot.Id, ((StaffelAntwort)abgabe.Value!).TeamId, "Die Nachricht hat bestimmt, für welches Team es zählt.");
            Assert.AreEqual(rot.Id, Stand.Geloest[0]);

            Assert.IsFalse(Gib(carl, new { turn = Stand.Zug, answer = "Venus", playerId = bert.Id, teamId = rot.Id }).Accepted,
                "Carl antwortet mit Berts Kennung an seiner Stelle.");
        }

        [TestMethod]
        public void TheParticipantsAreFixedAtTheStartAndOnlyTheyPlay()
        {
            Starte();

            Assert.IsTrue(spiel.IsLockedFor(dora), "Doras Telefon ohne Team ist nicht gesperrt.");
            Assert.AreEqual("Beim Start warst du in keinem Team oder nicht verbunden – diesmal läufst du nicht mit.",
                ((MinigameClientInfo)spiel.InfoFor(dora)).WatcherText);
            Assert.IsFalse(spiel.IsLockedFor(bert), "Bert ist gesperrt, nur weil er nicht dran ist - sein Telefon startete neu.");
            Assert.IsFalse(spiel.IsLockedFor(anna));

            // Emil kommt nach dem Start: er läuft nicht mit, auch nicht wenn Blau dran ist.
            emil.ConnectionState = PlayerConnection.Connected;
            Assert.IsTrue(spiel.IsLockedFor(emil), "Emil läuft mit, obwohl er beim Start getrennt war.");
            CollectionAssert.AreEquivalent(new[] { anna.Id, bert.Id, carl.Id, fritz.Id }, spiel.Lenke(r => ((RelayRules)r!).Teilnehmer.Keys.ToList()));

            // Bert trennt sich: nach Annas Treffer läuft sie den nächsten Teil wieder selbst.
            bert.ConnectionState = PlayerConnection.Disconnected;
            Assert.IsTrue(Antworte(anna, "Merkur").Accepted);
            Assert.AreEqual(anna.Id, Stand.Laeufer, "Der getrennte Bert ist dran.");
        }

        // --- Das Geheimnis ---

        private string AlleTelefone() => string.Join("|", new[] { anna, bert, carl, fritz, dora, emil, null }
            .Select(p => JsonSerializer.Serialize(spiel.InfoFor(p))));

        /// <summary>Gegenprobe: dieselbe Suche findet die Lösungen nach der Runde - sie misst also etwas.</summary>
        [TestMethod]
        public void NoSolutionReachesAPhoneBeforeTheResolution()
        {
            Starte(anna, "Merkur", "Venus", "Erde");

            void KeineLoesung(string wann)
            {
                var alle = AlleTelefone();
                foreach (var loesung in new[] { "Merkur", "Venus", "Erde", "Plutoo" })
                    Assert.IsFalse(alle.Contains(loesung, StringComparison.Ordinal), $"{wann}: „{loesung}“ steht an einem Telefon.");
            }

            KeineLoesung("Nach dem Start");
            Assert.IsTrue(Antworte(anna, "Merkur").Accepted);
            KeineLoesung("Nach einem Treffer");
            Assert.IsTrue(Antworte(bert, "Plutoo").Accepted);
            KeineLoesung("Nach einem Fehler");
            Assert.IsTrue(spiel.Lenke(r => ((RelayRules)r!).LasseGelten()));
            KeineLoesung("Nach dem Gelten lassen");
            Assert.IsTrue(spiel.Lenke(r => ((RelayRules)r!).GibWeiter()));
            KeineLoesung("Nach dem Weitergeben");

            Assert.IsNotNull(spiel.CloseRound());
            var danach = AlleTelefone();
            Assert.IsTrue(new[] { "Merkur", "Venus", "Erde" }.All(danach.Contains), "Nach der Runde fehlt die Auflösung.");
            Assert.IsFalse(danach.Contains("Plutoo"), "Eine abgelehnte Antwort steht an einem Telefon.");

            var aufloesung = Info(carl).Resolution;
            Assert.AreEqual(3, aufloesung.Count);
            Assert.IsTrue(aufloesung[0].Team?.EndsWith("Rot") == true && aufloesung[1].Team?.EndsWith("Rot") == true, "Rot hat die ersten beiden nicht.");
            Assert.IsNull(aufloesung[2].Team, "Der offene Teil gehört jemandem.");
        }

        [TestMethod]
        public void EveryPhoneSeesWhoRunsOnlyTheRunnerGetsTheTurn()
        {
            Starte();

            var laeufer = Info(anna);
            Assert.AreEqual((true, 1, 1, 6), (laeufer.IsRunner, laeufer.Turn, laeufer.Part, laeufer.TotalParts));

            var kollege = Info(bert);
            Assert.AreEqual((false, 0, 1), (kollege.IsRunner, kollege.Turn, kollege.Part), "Bert bekommt den Zug.");
            Assert.AreEqual("Anna", kollege.RunnerName);
            StringAssert.EndsWith(kollege.RunnerTeam, "Rot");
            Assert.AreEqual(0, Info(dora).Turn, "Dora ohne Team bekommt den Zug.");
            Assert.AreEqual(0, Info(null).Turn);

            Assert.IsTrue(Antworte(anna, "Pluto").Accepted);
            var gegner = Info(carl);
            Assert.AreEqual((true, false, 1), (gegner.IsRunner, gegner.LastCorrect, gegner.LastPart), "Carl erfährt nicht, dass er nach Rots Fehler dran ist.");
            StringAssert.EndsWith(gegner.LastTeam, "Rot");
            Assert.IsTrue(gegner.Teams.Single(t => t.Name == "Blau") is { IsOwn: true, IsOnTurn: true, Solved: 0 });
        }

        // --- Die Grenzen ---

        /// <summary>Carl läuft für Blau allein und kennt alle Teile: fünf Antworten in zehn Sekunden, die sechste erst danach (Entwurf Abschnitt 4).</summary>
        [TestMethod]
        public void AtMostFiveAnswersInTenSecondsFromOnePhone()
        {
            fritz.ConnectionState = PlayerConnection.Disconnected;
            Starte(carl, [.. Enumerable.Range(1, 8).Select(i => $"Teil{i}")]);

            for (var i = 1; i <= 5; i++)
                Assert.IsTrue(Antworte(carl, $"Teil{i}").Accepted, $"Carls {i}. Antwort zählt nicht.");

            Assert.IsFalse(Antworte(carl, "Teil6").Accepted, "Die sechste Antwort in derselben Sekunde zählt.");

            uhr.Vor(TimeSpan.FromSeconds(9.9));
            Assert.IsFalse(Antworte(carl, "Teil6").Accepted, "Nach 9,9 Sekunden ist das Fenster schon frei.");

            uhr.Vor(TimeSpan.FromSeconds(0.1));
            Assert.IsTrue(Antworte(carl, "Teil6").Accepted, "Nach zehn Sekunden zählt Carls Antwort wieder nicht.");
        }

        [TestMethod]
        public void AnAnswerIsCutToEightyCharacters()
        {
            Starte();

            var abgabe = Antworte(anna, new string('x', 32 * 1024));

            Assert.IsTrue(abgabe.Accepted);
            Assert.AreEqual(80, ((StaffelAntwort)abgabe.Value!).Text.Length, "Eine Antwort von 32 KB geht ungekürzt an die Spielleitung.");
        }

        // --- Ende und Eingriffe ---

        [TestMethod]
        public void TheRoundClosesAfterTheLastPart()
        {
            Starte(anna, "Merkur", "Venus", "Erde");

            Assert.IsNull(Antworte(anna, "Merkur").Closed);
            Assert.IsNull(Antworte(bert, "Venus").Closed);
            var letzte = Antworte(anna, "Erde");

            Assert.IsNotNull(letzte.Closed, "Nach dem letzten Teil bleibt die Runde offen.");
            Assert.IsTrue(letzte.Closed.Finished);
            Assert.AreEqual(MinigamePhase.Finished, spiel.Phase);
            Assert.IsTrue(Info(bert).IsWinner && !Info(carl).IsWinner, "Rot gewinnt nicht.");
            Assert.IsTrue(Info(anna).Teams.Single(t => t.Name == "Rot").IsWinner);

            // Ein Abbruch nach dem Schluss nimmt die gewertete Staffel nicht zurück.
            Assert.IsNotNull(spiel.Abort());
            Assert.IsTrue(spiel.Lenke(r => ((RelayRules)r!).Aufgeloest), "Der Abbruch nach dem Schluss hat die Wertung verworfen.");
            Assert.AreEqual(3, Info(carl).Resolution.Count, "Der Abbruch nach dem Schluss nimmt die Auflösung von den Telefonen.");
        }

        [TestMethod]
        public void TheGameMasterAcceptsTheLastMissEvenAfterTheClose()
        {
            Starte(anna, "Merkur", "Venus", "Erde");
            Assert.IsTrue(Antworte(anna, "Merkur").Accepted);
            Assert.IsTrue(Antworte(bert, "Venus").Accepted);
            Assert.IsTrue(Antworte(anna, "Erdee").Accepted);
            Assert.IsNotNull(Antworte(carl, "Mond").Closed, "Haben alle den letzten Teil verfehlt, bleibt die Runde offen.");

            Assert.IsFalse(spiel.Lenke(r => ((RelayRules)r!).GibWeiter()), "Nach dem Schluss gibt die Spielleitung weiter.");
            Assert.IsTrue(spiel.Lenke(r => ((RelayRules)r!).KannGelten));
            Assert.IsTrue(spiel.Lenke(r => ((RelayRules)r!).LasseGelten()), "Nach dem Schluss lässt sich Carls Antwort nicht gelten lassen.");
            Assert.AreEqual(blau.Id, Stand.Geloest[2]);
            Assert.IsFalse(spiel.Lenke(r => ((RelayRules)r!).LasseGelten()), "Dieselbe Antwort gilt zweimal.");
        }

        [TestMethod]
        public void AfterTheAbortNothingCanBeAccepted()
        {
            Starte();
            Assert.IsTrue(Antworte(anna, "Pluto").Accepted);

            Assert.IsNotNull(spiel.Abort());

            Assert.IsFalse(spiel.Lenke(r => ((RelayRules)r!).LasseGelten()), "Nach dem Abbruch lässt sich eine Antwort gelten lassen.");
            Assert.AreEqual(0, Info(anna).Resolution.Count, "Nach dem Abbruch stehen die Lösungen am Telefon.");
        }
    }
}
