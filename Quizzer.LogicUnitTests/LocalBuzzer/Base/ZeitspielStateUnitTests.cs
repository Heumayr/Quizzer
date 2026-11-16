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
    /// Tippsprint und Stoppuhr im Minispiel-Rahmen (M35 C1), ohne Netz: Zwischenstaende nur steigend,
    /// abgegeben erst mit dem Endstand, Doppel- und Fremdabgaben verworfen, Plausibilitaet am Server.
    /// </summary>
    [TestClass]
    public class ZeitspielStateUnitTests
    {
        private Player anna = null!;
        private Player bert = null!;
        private MinigameState spiel = null!;

        [TestInitialize]
        public void Aufbau()
        {
            anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", DisplayName = "Anna", ConnectionState = PlayerConnection.Connected };
            bert = new Player { Id = Guid.NewGuid(), Designation = "Bert", DisplayName = "Bert", ConnectionState = PlayerConnection.Connected };

            var game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { anna, bert })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            spiel = new MinigameState(new GameAccessor { GetGame = () => game });
        }

        private void Starte(MinigameKind art, int dauer = 10)
        {
            spiel.Configure(new MinigameSetup(art, "Spiel", "Regel", TotalRounds: 1, DurationSeconds: dauer));
            spiel.Reset();
            Assert.IsTrue(spiel.StartNextRound(), "Die Runde liess sich nicht starten.");
        }

        private MinigameSubmitResult Gib(Player p, MinigameKind art, object nutzlast, int? durchgang = null)
            => spiel.Submit(p, new MinigameAction
            {
                Kind = art,
                Durchgang = durchgang ?? spiel.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private MinigameSubmitResult Stand(Player p, int tipps, int ms, bool ende = false)
            => Gib(p, MinigameKind.TapSprint, new { taps = tipps, elapsedMs = ms, isFinal = ende });

        private MinigameSubmitResult Stopp(Player p, int? ms) => Gib(p, MinigameKind.Stopwatch, new { milliseconds = ms });

        private T Info<T>(Player? p) => (T)((MinigameClientInfo)spiel.InfoFor(p)).KindInfo!;

        // --- Tippsprint ---

        [TestMethod]
        public void IntermediateStandsOnlyRiseAndDoNotCountAsASubmission()
        {
            Starte(MinigameKind.TapSprint);

            Assert.IsTrue(Stand(anna, 8, 500).Accepted, "Ein plausibler Zwischenstand wurde verworfen.");
            Assert.IsTrue(Stand(anna, 15, 1000).Accepted, "Ein zweiter Zwischenstand wurde verworfen - der Rahmen hielt ihn fuer eine Doppelabgabe.");
            Assert.IsFalse(Stand(anna, 14, 1500).Accepted, "Ein sinkender Zwischenstand wurde angenommen.");
            Assert.IsFalse(Stand(anna, 40, 1500).Accepted, "Mehr als 20 Tipps je Sekunde wurden angenommen.");

            Assert.IsFalse(spiel.IsLockedFor(anna), "Nach einem Zwischenstand ist das Telefon gesperrt.");
            Assert.IsFalse(((MinigameClientInfo)spiel.InfoFor(anna)).OwnSubmitted, "Ein Zwischenstand gilt als Abgabe.");
            Assert.AreEqual(15, Info<TapSprintClientInfo>(anna).OwnTaps, "Der letzte angenommene Stand kommt nicht zurueck.");
            Assert.AreEqual(1000, Info<TapSprintClientInfo>(anna).OwnElapsedMs);
            Assert.AreEqual(0, spiel.Rules!.CurrentSubmissions.Count, "Ein Zwischenstand steht unter den Abgaben der Runde.");
        }

        [TestMethod]
        public void TheFinalLocksAndClosesTheRoundWhenAllAreDone()
        {
            Starte(MinigameKind.TapSprint);
            Stand(anna, 30, 2000);

            Assert.IsFalse(Stand(anna, 29, 10000, ende: true).Accepted, "Der Endstand liegt unter dem letzten Zwischenstand.");

            var endstand = Stand(anna, 61, 10000, ende: true);
            Assert.IsTrue(endstand.Accepted);
            Assert.IsNull(endstand.Closed, "Die Runde schliesst, obwohl Bert noch tippt.");
            Assert.IsTrue(spiel.IsLockedFor(anna), "Nach dem Endstand ist das Telefon offen.");
            Assert.IsFalse(Stand(anna, 62, 10000, ende: true).Accepted, "Ein zweiter Endstand wurde angenommen.");
            Assert.IsFalse(Stand(anna, 62, 10000).Accepted, "Nach dem Endstand kam noch ein Zwischenstand an.");

            var letzter = Stand(bert, 55, 10000, ende: true);

            Assert.IsNotNull(letzter.Closed, "Die Runde schliesst nicht, obwohl alle fertig sind.");
            Assert.AreEqual(61, ((TippsprintStand)letzter.Closed!.Submissions[anna.Id]).Tipps);
            Assert.AreEqual(MinigamePhase.Finished, spiel.Phase);

            var info = Info<TapSprintClientInfo>(bert);
            Assert.IsFalse(info.IsWinner);
            CollectionAssert.AreEqual(new[] { "Anna" }, info.WinnerNames);
            Assert.AreEqual(61, info.WinnerTaps);
            Assert.AreEqual(10, info.DurationSeconds);
            Assert.AreEqual(TippsprintEvaluator.MaxTapsPerSecond, info.MaxTapsPerSecond);
        }

        [TestMethod]
        public void AForeignKindOrAnOldDurchgangIsDiscarded()
        {
            Starte(MinigameKind.TapSprint);

            Assert.IsFalse(Gib(anna, MinigameKind.Reaction, new { taps = 5, elapsedMs = 500, isFinal = false }).Accepted,
                "Eine Abgabe fuer eine andere Art wurde angenommen.");
            Assert.IsFalse(Gib(anna, MinigameKind.TapSprint, new { taps = 5, elapsedMs = 500, isFinal = false }, spiel.Durchgang - 1).Accepted,
                "Ein Stand aus einem alten Durchgang wurde angenommen.");
            Assert.IsFalse(Stand(anna, 5, 10001).Accepted, "Laenger als die Dauer gemessen und angenommen.");
            Assert.IsFalse(Gib(anna, MinigameKind.TapSprint, "kaputt").Accepted, "Eine kaputte Nutzlast wurde angenommen.");
        }

        [TestMethod]
        public void ARepeatForgetsTheStands()
        {
            Starte(MinigameKind.TapSprint);
            Stand(anna, 30, 10000, ende: true);

            Assert.IsTrue(spiel.RepeatRound());

            Assert.IsFalse(spiel.IsLockedFor(anna), "Nach der Wiederholung bleibt der alte Endstand stehen.");
            Assert.IsTrue(Stand(anna, 5, 500).Accepted, "Nach der Wiederholung zaehlt der alte Stand als Untergrenze.");
        }

        // --- Stoppuhr ---

        [TestMethod]
        public void AStopCountsOnceAndAnUnusableOneCanBeResent()
        {
            Starte(MinigameKind.Stopwatch);

            Assert.IsFalse(Stopp(anna, -5).Accepted, "Eine negative Zeit wurde angenommen.");
            Assert.IsFalse(Stopp(anna, null).Accepted, "Eine fehlende Zeit wurde angenommen.");
            Assert.IsFalse(spiel.IsLockedFor(anna), "Eine verworfene Zeit sperrt das Telefon.");

            Assert.IsTrue(Stopp(anna, 10230).Accepted);
            Assert.IsFalse(Stopp(anna, 10000).Accepted, "Eine zweite Zeit wurde angenommen.");
            Assert.IsTrue(spiel.IsLockedFor(anna));

            var info = Info<StopwatchClientInfo>(anna);
            Assert.AreEqual(10230, info.OwnMilliseconds);
            Assert.AreEqual(230, info.OwnDistanceMs);
            Assert.AreEqual(10, info.TargetSeconds);
            Assert.AreEqual(StoppuhrEvaluator.VisibleMs, info.VisibleMs);
            Assert.AreEqual(20000, info.MaxMs);
        }

        [TestMethod]
        public void TooLateIsTakenButNotScoredAndTheClosestWins()
        {
            Starte(MinigameKind.Stopwatch);

            Assert.IsTrue(Stopp(anna, 25000).Accepted, "Zu spaet wurde verworfen - dann haengt die Runde am Aufgeben.");
            Assert.IsNull(Info<StopwatchClientInfo>(anna).OwnDistanceMs, "Zu spaet hat einen Abstand.");

            var letzter = Stopp(bert, 14000);

            Assert.IsNotNull(letzter.Closed, "Die Runde schliesst nicht, obwohl alle gestoppt haben.");

            var info = Info<StopwatchClientInfo>(anna);
            CollectionAssert.AreEqual(new[] { "Bert" }, info.WinnerNames);
            Assert.AreEqual(4000, info.WinnerDistanceMs);
            Assert.IsFalse(info.IsWinner);
            Assert.IsTrue(Info<StopwatchClientInfo>(bert).IsWinner);
        }
    }
}
