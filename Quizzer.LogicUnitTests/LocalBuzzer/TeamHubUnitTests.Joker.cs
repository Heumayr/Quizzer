using LocalBuzzer.Service.Base.States.Joker;
using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using static Quizzer.TestShared.TestNetwork;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>Die Joker-Sperre gegen einen Teamkollegen über den echten Hub (M37 T4) - mit einer fremden <c>teamId</c> in der Nutzlast.</summary>
    public partial class TeamHubUnitTests
    {
        [TestMethod]
        public async Task AJokerBlockAgainstATeammateIsRejectedAtTheHub()
        {
            var sperre = new JokerOffer(Guid.NewGuid(), JokerKind.Block);
            var abgaben = new List<MinigameSubmission>();
            Steuerung.EventBus.MinigameSubmitted += x => { lock (abgaben) abgaben.Add(x); };
            var a = await VerbindeAsync(anna);

            Steuerung.StateManager.MinigameState.Configure(new MinigameSetup(default, "Joker-Runde", "Regel", 1, CountdownSeconds: 0,
                Jokers: new Dictionary<Guid, IReadOnlyList<JokerOffer>> { [anna.Id] = [sperre] }));
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.Minigame);
            Assert.IsTrue(await Steuerung.StartMinigameRoundAsync());

            Task SendeAsync(Guid ziel) => a.Verbindung.InvokeAsync("SpielAktion", new
            {
                kind = 0,
                prePhase = (int)MinigamePrePhase.Joker,
                durchgang = Steuerung.StateManager.MinigameState.Durchgang,
                payload = new { jokerId = sperre.Id, target = ziel, teamId = blau.Id },
            });

            await SendeAsync(bert.Id);
            await Task.Delay(200);
            lock (abgaben) Assert.AreEqual(0, abgaben.Count, "Anna hat ihren Teamkollegen Bert gesperrt - die teamId in der Nutzlast zählte.");

            await SendeAsync(carl.Id);
            Assert.IsTrue(await WarteAsync(() => { lock (abgaben) return abgaben.Count == 1; }), "Die Sperre gegen Carl kam nicht an.");
            lock (abgaben) Assert.AreEqual(new JokerChoice(anna.Id, sperre.Id, JokerKind.Block, carl.Id), abgaben[0].Value);
        }
    }
}
