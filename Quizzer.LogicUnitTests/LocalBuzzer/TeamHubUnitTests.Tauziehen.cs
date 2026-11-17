using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;
using static Quizzer.TestShared.TestNetwork;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Tauziehen über den echten Hub (M37 T9), die Serveruhr zum Vorstellen: wer ohne Team ist, zieht nicht mit, und seine
    /// Abgabe erreicht die Spielleitung nicht; eine fremde <c>playerId</c> in der Nutzlast zählt für den Absender; jedes Telefon
    /// bekommt mit seiner Antwort das Seil, und die Fristwache schliesst die Runde an der Serveruhr - ohne einen Endstand.
    /// </summary>
    public partial class TeamHubUnitTests
    {
        private readonly List<MinigameSubmission> zuege = new();
        private readonly List<MinigameRoundClosed> zugeschlossen = new();

        private async Task<StellbareUhr> TauziehenAsync()
        {
            var uhr = new StellbareUhr();
            var zustand = Steuerung.StateManager.MinigameState;
            zustand.Uhr = uhr;
            Steuerung.EventBus.MinigameSubmitted += z => { lock (zuege) zuege.Add(z); };
            Steuerung.EventBus.MinigameRoundClosed += r => { lock (zugeschlossen) zugeschlossen.Add(r); };

            zustand.Configure(new MinigameSetup(MinigameKind.TugOfWar, "Tauziehen", "Regel", 1, DurationSeconds: 5, TeamScoring: TeamMinigameScoring.Sum));
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await Steuerung.StartMinigameRoundAsync(), "Das Tauziehen liess sich nicht starten.");
            uhr.Vor(TimeSpan.FromSeconds(4));
            return uhr;
        }

        private Task ZieheAsync(Telefon t, int tipps, Guid? fuer = null)
            => t.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = (int)MinigameKind.TugOfWar,
                durchgang = Steuerung.StateManager.MinigameState.Durchgang,
                payload = new { taps = tipps, isFinal = false, playerId = fuer },
            });

        private static JsonElement? Ziehen(Telefon t)
            => Letzter(t)?.LayoutInfo is JsonElement info && info.TryGetProperty("kindInfo", out var art) && art.ValueKind == JsonValueKind.Object ? art : null;

        private int Zahl<T>(List<T> liste)
        {
            lock (liste) return liste.Count;
        }

        [TestMethod]
        public async Task TheTugOfWarRunsOverTheHubAndTheServerClockClosesIt()
        {
            var a = await VerbindeAsync(anna);
            var c = await VerbindeAsync(carl);
            var d = await VerbindeAsync(dora);
            var uhr = await TauziehenAsync();

            Assert.IsTrue(await WarteAsync(() => Letzter(d)?.CurrentLayoutLocked == true), "Doras Telefon ohne Team ist nicht gesperrt.");

            await ZieheAsync(a, 10);
            await ZieheAsync(c, 15);
            Assert.IsTrue(await WarteAsync(() => Zahl(zuege) == 2), "Die Stände erreichen die Spielleitung nicht.");
            Assert.IsTrue(await WarteAsync(() => Ziehen(a)?.GetProperty("rope").ValueKind == JsonValueKind.Number
                                                 && Ziehen(a)!.Value.GetProperty("ownTaps").GetInt32() == 10),
                "Anna bekommt mit ihrer Antwort weder ihren Stand noch das Seil zurück.");

            // Dora ohne Team: verworfen, die Spielleitung sieht nichts.
            await ZieheAsync(d, 5);
            await Task.Delay(200);
            Assert.AreEqual(2, Zahl(zuege), "Doras Abgabe ohne Team erreichte die Spielleitung.");

            // Carl schickt Annas Kennung mit - es zählt trotzdem für Carl und Blau.
            await ZieheAsync(c, 18, fuer: anna.Id);
            Assert.IsTrue(await WarteAsync(() => Zahl(zuege) == 3));
            MinigameSubmission letzte;
            lock (zuege) letzte = zuege[^1];
            Assert.AreEqual(carl.Id, letzte.Player.Id, "Die Nachricht hat bestimmt, wer zieht.");
            Assert.AreEqual(blau.Id, ((TauziehenZug)letzte.Value).TeamId);

            // Kein Endstand: die Fristwache schliesst an der Serveruhr, nach Dauer und Nachlauf.
            await Task.Delay(200);
            Assert.AreEqual(0, Zahl(zugeschlossen), "Die Runde schloss vor der Frist.");
            uhr.Vor(TimeSpan.FromSeconds(6));

            Assert.IsTrue(await WarteAsync(() => Zahl(zugeschlossen) == 1), "Die Fristwache schliesst das Tauziehen nicht.");
            Assert.IsTrue(await WarteAsync(() => Ziehen(c)?.GetProperty("isWinner").GetBoolean() == true), "Carl erfährt nicht, dass Blau gewonnen hat.");
            Assert.IsFalse(Ziehen(a)!.Value.GetProperty("isWinner").GetBoolean());
        }
    }
}
