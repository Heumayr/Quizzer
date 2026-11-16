using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Tippsprint und Stoppuhr ueber den echten Hub (M35 C1): die Zwischenstaende erreichen den
    /// Spielleiter, ein sinkender nicht; der Endstand schliesst die Runde; wer abgibt, sagt die Verbindung.
    /// </summary>
    public partial class MinispielHubUnitTests
    {
        private async Task StarteAsync(MinigameKind art, int dauer)
        {
            var ctrl = server.BuzzerController!;
            ctrl.StateManager.MinigameState.Configure(new MinigameSetup(art, "Spiel", "Regel", 1, DurationSeconds: dauer));
            await ctrl.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await ctrl.StartMinigameRoundAsync(), "Die Runde liess sich nicht starten.");
        }

        private Task SendeAsync(Telefon telefon, MinigameKind art, object nutzlast, Guid? fuer = null)
            => telefon.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)art,
                durchgang = server.BuzzerController!.StateManager.MinigameState.Durchgang,
                payload = nutzlast,
                playerId = fuer,
            });

        private Task StandAsync(Telefon telefon, int tipps, int ms, bool ende = false, Guid? fuer = null)
            => SendeAsync(telefon, MinigameKind.TapSprint, new { taps = tipps, elapsedMs = ms, isFinal = ende }, fuer);

        private List<MinigameSubmission> Abgaben() { lock (abgaben) return abgaben.ToList(); }

        [TestMethod]
        public async Task TheTapSprintRaceRunsOverTheHub()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await StarteAsync(MinigameKind.TapSprint, dauer: 5);

            await StandAsync(a, 6, 500);
            await StandAsync(a, 12, 1000);
            await StandAsync(a, 11, 1500);

            Assert.IsTrue(await WarteAsync(() => Anzahl(abgaben) == 2), "Die Zwischenstaende erreichen den Spielleiter nicht.");
            await Task.Delay(300);
            Assert.AreEqual(2, Anzahl(abgaben), "Ein sinkender Zwischenstand erreichte den Spielleiter.");
            Assert.AreEqual(12, ((TippsprintStand)Abgaben()[1].Value).Tipps);
            Assert.IsFalse(Letzter(a.Zustaende)!.CurrentLayoutLocked, "Nach einem Zwischenstand ist Annas Telefon gesperrt.");

            // Fremdabgabe: Bert schickt Annas Id mit - der Stand gilt trotzdem fuer Bert.
            await StandAsync(b, 4, 500, fuer: anna.Id);
            Assert.IsTrue(await WarteAsync(() => Anzahl(abgaben) == 3));
            Assert.AreEqual(bert.Id, Abgaben()[2].Player.Id, "Die Nachricht hat bestimmt, fuer wen getippt wird.");

            await StandAsync(a, 40, 5000, ende: true);
            Assert.IsTrue(await WarteAsync(() => Abgegeben(Letzter(a.Zustaende))), "Anna bekommt ihren Endstand nicht zurueck.");
            Assert.AreEqual(0, Anzahl(geschlossen), "Die Runde schliesst, obwohl Bert noch tippt.");

            await StandAsync(b, 35, 5000, ende: true);

            Assert.IsTrue(await WarteAsync(() => Anzahl(geschlossen) == 1), "Nach dem letzten Endstand schliesst die Runde nicht.");
            MinigameRoundClosed runde;
            lock (geschlossen) runde = geschlossen[0];
            Assert.AreEqual(2, runde.Submissions.Count);
            Assert.AreEqual(40, ((TippsprintStand)runde.Submissions[anna.Id]).Tipps);
            Assert.IsTrue(runde.Submissions.Values.All(v => v is TippsprintStand { Ende: true }), "Ein Zwischenstand steht unter den Abgaben.");
        }

        [TestMethod]
        public async Task TheStopwatchTakesOneStopPerPhone()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await StarteAsync(MinigameKind.Stopwatch, dauer: 10);

            await SendeAsync(a, MinigameKind.Stopwatch, new { milliseconds = 10230 });
            await SendeAsync(a, MinigameKind.Stopwatch, new { milliseconds = 10000 });

            Assert.IsTrue(await WarteAsync(() => Abgegeben(Letzter(a.Zustaende))), "Anna bekommt ihre Zeit nicht zurueck.");
            await Task.Delay(300);
            Assert.AreEqual(1, Anzahl(abgaben), "Eine zweite Zeit derselben Runde erreichte den Spielleiter.");
            Assert.AreEqual(230, Info(Letzter(a.Zustaende)).GetProperty("kindInfo").GetProperty("ownDistanceMs").GetInt32());

            await SendeAsync(b, MinigameKind.Stopwatch, new { milliseconds = 9900 });

            Assert.IsTrue(await WarteAsync(() => Anzahl(geschlossen) == 1), "Nach der letzten Zeit schliesst die Runde nicht.");
            Assert.IsTrue(await WarteAsync(() => Info(Letzter(b.Zustaende)).GetProperty("kindInfo").GetProperty("isWinner").GetBoolean()),
                "Bert erfaehrt nicht, dass er gewonnen hat.");
        }
    }
}
