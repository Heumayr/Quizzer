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
    /// Das Tauziehen am Server (M37 T9) mit vorstellbarer Serveruhr - die Missbrauchsfälle des Entwurfs: Tippraten über der
    /// Grenze, eine Abgabe, wer nicht mitzieht (ohne Team, beim Start getrennt), ein Team oder Spieler in der Nutzlast, ein Stand
    /// im Countdown oder nach der Frist. Dazu: die Fristwache schließt an der Serveruhr, wer nie tippt, zählt im Durchschnitt mit,
    /// und jedes Telefon sieht das Seil und sein Team.
    /// </summary>
    [TestClass]
    public class TauziehenStateUnitTests
    {
        private static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);
        private const int Dauer = 5;

        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private Player dora = null!;
        private Player emil = null!;
        private Game game = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;
        private StellbareUhr uhr = null!;
        private TeamRoster aufstellung = TeamRoster.Empty;
        private MinigameState spiel = null!;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");
            dora = Spieler("Dora");
            emil = Spieler("Emil");
            uhr = new StellbareUhr();
            game = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Rot", ColorIndex = 5 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Blau", ColorIndex = 4 };
            game.GameTeams.AddRange([rot, blau]);

            // Anna und Bert sind Rot, Carl ist Blau, Dora ohne Team; Emil ist Blau, aber beim Start getrennt.
            foreach (var (p, team) in new[] { (anna, rot), (bert, rot), (carl, blau), (dora, (GameTeam?)null), (emil, blau) })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p, GameTeamId = team?.Id });

            emil.ConnectionState = PlayerConnection.Disconnected;
            aufstellung = TeamRoster.From(game);

            spiel = new MinigameState(new GameAccessor { GetGame = () => game }, () => aufstellung) { Uhr = uhr };
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private void Starte(TeamMinigameScoring wertung = TeamMinigameScoring.Average, bool nachDemCountdown = true)
        {
            spiel.Configure(new MinigameSetup(MinigameKind.TugOfWar, "Tauziehen", "Regel", 1, DurationSeconds: Dauer, TeamScoring: wertung));
            spiel.Reset();
            Assert.IsTrue(spiel.StartNextRound(), "Das Tauziehen liess sich nicht starten.");

            if (nachDemCountdown)
                uhr.Vor(Vorlauf);
        }

        private MinigameSubmitResult Gib(Player p, object nutzlast)
            => spiel.Submit(p, new MinigameAction
            {
                Kind = MinigameKind.TugOfWar,
                Durchgang = spiel.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private MinigameSubmitResult Stand(Player p, int tipps, bool ende = false) => Gib(p, new { taps = tipps, isFinal = ende });

        private TugOfWarClientInfo Info(Player? p) => (TugOfWarClientInfo)((MinigameClientInfo)spiel.InfoFor(p)).KindInfo!;

        private TugOfWarRules Regeln => (TugOfWarRules)spiel.Rules!;

        [TestMethod]
        public void OnlyTeamMembersConnectedAtTheStartPull()
        {
            Starte();

            CollectionAssert.AreEquivalent(new[] { anna.Id, bert.Id, carl.Id }, Regeln.Teilnehmer.Keys.ToList());
            Assert.AreEqual(rot.Id, Regeln.Teilnehmer[anna.Id], "Anna zieht nicht für Rot.");

            uhr.Vor(TimeSpan.FromSeconds(1));
            Assert.IsFalse(Stand(dora, 5).Accepted, "Dora ohne Team hat mitgezogen.");
            Assert.IsTrue(spiel.IsLockedFor(dora), "Doras Telefon ist nicht gesperrt.");
            StringAssert.Contains(((MinigameClientInfo)spiel.InfoFor(dora)).WatcherText, "ziehst du nicht mit");

            // Emil kommt nach dem Start wieder - er schaut zu, die Runde gehört denen, die beim Start da waren.
            emil.ConnectionState = PlayerConnection.Connected;
            Assert.IsFalse(Stand(emil, 5).Accepted, "Emil zog mit, obwohl er beim Start getrennt war.");
            Assert.IsTrue(Stand(anna, 5).Accepted, "Anna aus Rot wurde verworfen.");
        }

        /// <summary>Die Regel selbst prüft die Teilnahme noch einmal - nicht nur der Rahmen über <c>Plays</c>.</summary>
        [TestMethod]
        public void TheRulesRejectANonParticipantOnTheirOwn()
        {
            Starte();
            uhr.Vor(TimeSpan.FromSeconds(1));

            Assert.IsNull(Regeln.Accept(dora, JsonSerializer.SerializeToElement(new { taps = 5, isFinal = false })), "Die Regel nahm Dora ohne Team an.");
            Assert.IsNotNull(Regeln.Accept(anna, JsonSerializer.SerializeToElement(new { taps = 5, isFinal = false })));
        }

        [TestMethod]
        public void TheServerClockDecidesTheRateAndTheWindow()
        {
            spiel.Configure(new MinigameSetup(MinigameKind.TugOfWar, "Tauziehen", "Regel", 1, DurationSeconds: Dauer));
            spiel.Reset();
            spiel.StartNextRound();

            uhr.Vor(TimeSpan.FromSeconds(2));
            Assert.IsFalse(Stand(anna, 0).Accepted, "Im Countdown wurde ein Stand angenommen.");

            uhr.Vor(TimeSpan.FromSeconds(2));
            Assert.IsFalse(Stand(anna, 21).Accepted, "21 Tipps nach einer Sekunde wurden angenommen.");
            Assert.IsTrue(Stand(anna, 20).Accepted, "20 Tipps nach einer Sekunde wurden verworfen.");
            Assert.IsFalse(Stand(anna, 19).Accepted, "Ein sinkender Stand wurde angenommen.");

            // Nach der Dauer: angenommen bis zum Ende des Nachlaufs, aber nie mehr als Grenze mal Dauer.
            uhr.Vor(TimeSpan.FromSeconds(Dauer));
            Assert.IsFalse(Stand(anna, 101).Accepted, "Im Nachlauf wurde über Grenze mal Dauer gezählt.");
            Assert.IsTrue(Stand(bert, 100).Accepted, "Der Stand im Nachlauf wurde verworfen.");
            Assert.IsNull(spiel.CloseIfDue(), "Die Runde schloss vor dem Ende des Nachlaufs.");

            uhr.Vor(TimeSpan.FromMilliseconds(TauziehenEvaluator.NachlaufMs));
            Assert.IsFalse(Stand(carl, 10).Accepted, "Nach Dauer und Nachlauf wurde noch angenommen.");
            Assert.IsNotNull(spiel.CloseIfDue(), "Die Fristwache schliesst die Runde nicht.");
        }

        /// <summary>Team und Spieler sagt die Aufstellung des Hubs, nie die Nachricht.</summary>
        [TestMethod]
        public void ATeamOrPlayerInThePayloadIsIgnored()
        {
            Starte();
            uhr.Vor(TimeSpan.FromSeconds(1));

            var ergebnis = Gib(anna, new { taps = 10, isFinal = false, teamId = blau.Id, playerId = carl.Id });

            Assert.IsTrue(ergebnis.Accepted);
            Assert.AreEqual(rot.Id, ((TauziehenZug)ergebnis.Value!).TeamId, "Die Nutzlast hat das Team bestimmt.");
            Assert.AreEqual(10, Regeln.Stand.Teams.Single(t => t.TeamId == rot.Id).Tipps, "Annas Tipps zählen nicht für Rot.");
            Assert.AreEqual(0, Regeln.Stand.Teams.Single(t => t.TeamId == blau.Id).Tipps, "Annas Tipps zählen für Blau.");
        }

        /// <summary>Die Runde schliesst, sobald alle, die mitziehen, ihren Endstand geschickt haben - Dora ohne Team hält sie nicht auf.</summary>
        [TestMethod]
        public void TheRoundClosesWhenEveryPullerHasSentTheFinal()
        {
            Starte();
            uhr.Vor(TimeSpan.FromSeconds(Dauer));

            Assert.IsNull(Stand(anna, 60, ende: true).Closed);
            Assert.IsNull(Stand(bert, 0, ende: true).Closed);
            Assert.IsFalse(Stand(anna, 61).Accepted, "Nach dem Endstand wurde noch angenommen.");

            var runde = Stand(carl, 40, ende: true).Closed;

            Assert.IsNotNull(runde, "Nach dem letzten Endstand schliesst die Runde nicht.");
            CollectionAssert.AreEquivalent(new[] { anna.Id, bert.Id, carl.Id }, runde!.Submissions.Keys.ToList());
            Assert.AreEqual(0, ((TauziehenZug)runde.Submissions[bert.Id]).Tipps);
        }

        /// <summary>Wer nie tippt, steht trotzdem unter den Abgaben - sonst hübe er im Durchschnitt sein Team.</summary>
        [TestMethod]
        public void APullerWithoutTapsCountsInTheAverage()
        {
            Starte();
            uhr.Vor(TimeSpan.FromSeconds(Dauer));
            Stand(anna, 60, ende: true);
            Stand(carl, 40, ende: true);

            var runde = spiel.CloseRound()!;
            var ergebnis = TauziehenEvaluator.Werte(
                runde.Submissions.ToDictionary(a => a.Key, a => (TauziehenZug)a.Value), TeamMinigameScoring.Average);

            Assert.AreEqual(2, ergebnis.Teams.Single(t => t.TeamId == rot.Id).Mitglieder, "Bert ohne Tipp fehlt im Durchschnitt.");
            CollectionAssert.AreEquivalent(new[] { blau.Id }, ergebnis.Gewinner.ToList(), "Rot gewinnt mit 30 im Schnitt gegen 40.");
        }

        [TestMethod]
        public void EachPhoneSeesTheRopeAndItsTeamTheWinnerOnlyAtTheEnd()
        {
            Starte(TeamMinigameScoring.Sum);
            uhr.Vor(TimeSpan.FromSeconds(1));
            Stand(anna, 10);
            Stand(carl, 20);

            var annas = Info(anna);
            Assert.AreEqual(10, annas.OwnTaps);
            Assert.AreEqual(1000, annas.ElapsedMs, "Die Laufzeit an der Serveruhr kommt nicht ans Telefon.");
            Assert.AreEqual(2, annas.Teams.Count);
            Assert.AreEqual("Rot", annas.Teams.Single(t => t.IsOwn).Name, "Anna erkennt ihr Team nicht.");
            Assert.AreEqual(5, annas.Teams.Single(t => t.IsOwn).ColorIndex);
            Assert.AreEqual("20 Tipps", annas.Teams.Single(t => !t.IsOwn).Pull);
            Assert.IsNotNull(annas.Rope, "Bei zwei Teams fehlt das Seil.");
            Assert.IsFalse(annas.IsWinner || annas.Teams.Any(t => t.IsWinner), "Während der Runde steht ein Sieger fest.");
            Assert.IsFalse(Info(carl).IsWinner, "Carl hat gewonnen, bevor die Runde zu ist - Blau führt nur.");
            Assert.IsFalse(Info(dora).Teams.Any(t => t.IsOwn), "Dora ohne Team hat ein eigenes Team am Seil.");

            uhr.Vor(TimeSpan.FromSeconds(Dauer));
            spiel.CloseRound();

            Assert.IsTrue(Info(carl).IsWinner, "Carl erfährt nicht, dass Blau gewonnen hat.");
            Assert.IsFalse(Info(anna).IsWinner);
            Assert.IsTrue(Info(anna).Teams.Single(t => !t.IsOwn).IsWinner, "Anna sieht nicht, wer gewonnen hat.");
        }

        [TestMethod]
        public void WithoutATeamGameNobodyPulls()
        {
            aufstellung = TeamRoster.Empty;
            Starte();
            uhr.Vor(TimeSpan.FromSeconds(1));

            Assert.AreEqual(0, Regeln.Teilnehmer.Count);
            Assert.IsFalse(Stand(anna, 5).Accepted, "Ohne Teamspiel zog Anna.");
            Assert.AreEqual(0, Info(anna).Teams.Count);
        }

        /// <summary>Wiederholen vergisst die Runde: neuer Start an der Serveruhr, neue Teilnehmer, keine alten Stände.</summary>
        [TestMethod]
        public void RepeatingStartsOverAtTheServerClock()
        {
            Starte();
            uhr.Vor(TimeSpan.FromSeconds(2));
            Assert.IsTrue(Stand(anna, 30).Accepted);

            dora.ConnectionState = PlayerConnection.Disconnected;
            Assert.IsTrue(spiel.RepeatRound());

            Assert.IsFalse(Stand(anna, 30).Accepted, "Nach dem Wiederholen zählt die alte Laufzeit.");
            uhr.Vor(Vorlauf + TimeSpan.FromSeconds(1));
            Assert.IsTrue(Stand(anna, 20).Accepted, "Nach dem Wiederholen blieb der alte Stand stehen.");
            Assert.AreEqual(20, Info(anna).OwnTaps);
        }

        /// <summary>
        /// V24: wer neu verbindet, liest an der Serveruhr, was vom Countdown bleibt und wie lange schon gezogen wird - mitten im
        /// Ziehen nichts mehr, im Countdown der Rest. Vorher stand dort immer der ganze Countdown, und das Telefon zählte neu.
        /// </summary>
        [TestMethod]
        public void APhoneReconnectingReadsOnlyWhatIsLeftOfTheCountdown()
        {
            Starte(nachDemCountdown: false);
            var rahmen = (MinigameClientInfo)spiel.InfoFor(anna);
            Assert.AreEqual(3000, rahmen.CountdownRemainingMs, "Zu Beginn des Durchgangs bleibt nicht der ganze Countdown.");

            uhr.Vor(TimeSpan.FromMilliseconds(1600));
            Assert.AreEqual(1400, ((MinigameClientInfo)spiel.InfoFor(anna)).CountdownRemainingMs, "Im Countdown zählt das Telefon nicht den Rest.");
            Assert.AreEqual(0, Info(anna).ElapsedMs);

            uhr.Vor(TimeSpan.FromMilliseconds(1400 + 4000));
            Assert.AreEqual(0, ((MinigameClientInfo)spiel.InfoFor(anna)).CountdownRemainingMs, "Mitten im Ziehen zählt das Telefon den Countdown neu.");
            Assert.AreEqual(4000, Info(anna).ElapsedMs);
            Assert.AreEqual(3, ((MinigameClientInfo)spiel.InfoFor(anna)).CountdownSeconds, "Der eingestellte Countdown ging verloren.");

            // Ein neuer Durchgang (Wiederholen) zählt wieder von vorn.
            Assert.IsTrue(spiel.RepeatRound());
            Assert.AreEqual(3000, ((MinigameClientInfo)spiel.InfoFor(anna)).CountdownRemainingMs, "Nach dem Wiederholen bleibt der alte Rest.");
        }
    }
}
