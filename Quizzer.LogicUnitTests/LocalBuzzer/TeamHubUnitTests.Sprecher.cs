using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using static Quizzer.TestShared.TestNetwork;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Teamberatung am Hub (M37 T5, Entwurf Abschnitt 4): das Telefon nennt den Sprecher, nur die Sprecher dürfen abgeben
    /// (<see cref="AnswerLock"/>), und eine Abgabe eines Nicht-Sprechers wird verworfen - auch wenn er die Kennung des
    /// Sprechers in die Nutzlast schreibt.
    /// </summary>
    public partial class TeamHubUnitTests
    {
        private const string BeratSatz = "Berate dich mit Anna – sie antwortet für Rot.";

        /// <summary>Wie das Fragefenster eine Teamberatung einstellt: Sprecher in der Aufstellung, nur sie antworten, Texteingabe.</summary>
        private async Task BeratungAsync()
        {
            await Steuerung.SetTeamsAsync(TeamRoster.From(game));
            await Steuerung.SetAnswerersAsync([anna.Id, carl.Id], new Dictionary<Guid, string> { [bert.Id] = BeratSatz });
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Input);
        }

        private int Eingaben(List<BuzzerInputState.InputResult> liste)
        {
            lock (liste) return liste.Count;
        }

        [TestMethod]
        public async Task EveryPhoneNamesTheSpeakerOfItsTeam()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);

            Assert.AreEqual("Anna", Letzter(a)!.Team?.Speaker);
            Assert.AreEqual("Anna", Letzter(b)!.Team?.Speaker, "Bert erfährt nicht, wer für Rot spricht.");
            Assert.AreEqual("Carl", Letzter(c)!.Team?.Speaker);

            rot.SpeakerTurn = 1;
            await Steuerung.SetTeamsAsync(TeamRoster.From(game));
            Assert.IsTrue(await WarteAsync(() => Letzter(a)!.Team?.Speaker == "Bert"), "Nach dem Sprecherwechsel zeigt Annas Telefon den alten.");
        }

        [TestMethod]
        public async Task ANonSpeakerIsToldToConsultAndHisAnswerIsDiscarded()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);
            var eingegangen = new List<BuzzerInputState.InputResult>();
            var alle = 0;
            Steuerung.EventBus.PlayerSubmittedInput += (r, _) => { lock (eingegangen) eingegangen.Add(r); };
            Steuerung.EventBus.AllPlayersSubmittedInput += (_, _) => Interlocked.Increment(ref alle);

            await BeratungAsync();

            Assert.IsTrue(await WarteAsync(() => Letzter(b) is { ExcludedFromQuestion: true, CurrentLayoutLocked: true }),
                "Berts Telefon ist offen, obwohl Anna für Rot spricht.");
            Assert.AreEqual(BeratSatz, Letzter(b)!.ExcludedReason);
            Assert.IsTrue(await WarteAsync(() => Letzter(a) is { ExcludedFromQuestion: false, CurrentLayoutLocked: false }), "Annas Eingabe ist nicht offen.");

            // --- Bert gibt ab, mit Annas Kennung in der Nutzlast: verworfen, nichts erreicht den Spielleiter ---
            await b.Verbindung.InvokeAsync("SubmitInput", new { playerId = anna.Id, value = "Paris" });
            await Task.Delay(200);
            Assert.AreEqual(0, Eingaben(eingegangen), "Die Abgabe eines Nicht-Sprechers kam beim Spielleiter an.");
            Assert.AreEqual(0, Steuerung.StateManager.BuzzerInputState.InputsForPlayer.Count, "Die Abgabe eines Nicht-Sprechers steht im Zustand.");

            // --- Die Sprecher geben ab: angenommen, und mit beiden ist die Runde zu - Bert hält sie nicht offen ---
            await a.Verbindung.InvokeAsync("SubmitInput", new { value = "Rom" });
            await c.Verbindung.InvokeAsync("SubmitInput", new { value = "Mailand" });

            Assert.IsTrue(await WarteAsync(() => Volatile.Read(ref alle) == 1), "Die Runde schließt nicht, obwohl beide Sprecher abgegeben haben.");
            lock (eingegangen)
                CollectionAssert.AreEquivalent(new[] { anna.Id, carl.Id }, eingegangen.Select(r => r.PlayerId).ToArray());
            Assert.AreEqual("Rom", Steuerung.StateManager.BuzzerInputState.InputsForPlayer[anna.Id].Value, "Annas Antwort wurde ersetzt.");
        }
    }
}
