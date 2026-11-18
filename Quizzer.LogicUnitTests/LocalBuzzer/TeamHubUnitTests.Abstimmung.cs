using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Buzzer;
using static Quizzer.TestShared.TestNetwork;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Team-Abstimmung am Hub (M37 T7, Entwurf Abschnitt 4, F-M37-13): vor dem Schluss sieht jedes Telefon nur die eigene Stimme
    /// und wie viele seines Teams abgestimmt haben, danach nur die Antwort des eigenen Teams. Ein Gegnertelefon, das mitten in
    /// der Runde neu verbindet, bekommt den vollen Zustand - und darin nichts vom anderen Team. Missbrauch: eine fremde Kennung
    /// in der Nutzlast, ein gesperrtes Mitglied, zu viele Stimmen.
    /// </summary>
    public partial class TeamHubUnitTests
    {
        private static readonly Dictionary<string, string> Tasten = new() { ["A"] = "Rom", ["B"] = "Paris", ["C"] = "Wien" };

        /// <summary>Wie das Fragefenster eine Team-Abstimmung einstellt: Tasten, alle Mitglieder stimmen ab, Abstimmung an.</summary>
        private async Task AbstimmungAsync(params Guid[] abstimmende)
        {
            Steuerung.StateManager.BuzzerKeySelector.Infos = new BuzzerKeySelector.BuzzerKeySelectorInfo { KeysAndDesignations = new(Tasten) };
            await Steuerung.SetTeamsAsync(TeamRoster.From(game));
            await Steuerung.SetAnswerersAsync(abstimmende.Length > 0 ? abstimmende : [anna.Id, bert.Id, carl.Id]);
            Steuerung.StartTeamVote();
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.KeySelect);
        }

        private static Task StimmeAsync(Telefon t, params string[] tasten)
            => t.Verbindung.InvokeAsync("SelectionResults", new { selectedKeys = tasten });

        private static TeamVoteClientInfo? Abstimmung(Telefon t) => Letzter(t)?.Team?.Vote;

        private static int Zahl(Telefon t)
        {
            lock (t.Staende) return t.Staende.Count;
        }

        [TestMethod]
        public async Task EachPhoneSeesOnlyItsOwnVoteAndHowManyOfItsTeamVoted()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            var c = await VerbindeAsync(carl);
            await AbstimmungAsync();

            Assert.IsTrue(await WarteAsync(() => Abstimmung(a) is { Voted: 0, Of: 2, Closed: false }), "Annas Telefon zeigt den Stand nicht: " + Abstimmung(a)?.Of);
            Assert.IsTrue(await WarteAsync(() => Abstimmung(c) is { Voted: 0, Of: 1 }), "Carls Telefon zählt nicht nur Blau.");

            await StimmeAsync(a, "B");

            Assert.IsTrue(await WarteAsync(() => Abstimmung(b)?.Voted == 1), "Bert erfährt nicht, dass jemand aus Rot abgestimmt hat.");
            Assert.AreEqual(0, Abstimmung(b)!.Own.Count, "Bert sieht eine Stimme, die nicht seine ist: " + string.Join(",", Abstimmung(b)!.Own));
            Assert.IsTrue(await WarteAsync(() => Abstimmung(a)?.Own.SequenceEqual(["B"]) == true), "Anna sieht ihre eigene Stimme nicht.");
            Assert.AreEqual("", Abstimmung(a)!.Result);

            // Eine Antwort, die vor dem Schluss käme, zeigt kein Telefon - erst wenn die Abstimmung zu ist.
            var annaVorher = Zahl(a);
            var carlVorher = Zahl(c) + 2;
            await Steuerung.SetTeamVoteResultsAsync(new Dictionary<Guid, string> { [rot.Id] = "B (1 von 2)" });
            Assert.IsTrue(await WarteAsync(() => Zahl(a) > annaVorher), "Anna bekam nach der Auswertung keinen Stand.");
            Assert.AreEqual("", Abstimmung(a)!.Result, "Vor dem Schluss steht die Antwort des Teams am Telefon.");
            await Steuerung.SetTeamVoteResultsAsync(new Dictionary<Guid, string>());
            Assert.IsTrue(await WarteAsync(() => Zahl(c) == carlVorher), "Carl bekam die beiden Stände der Auswertung nicht.");

            // Das Gegnertelefon erfährt nicht einmal, dass abgestimmt wurde - es bekommt keinen neuen Stand.
            await StimmeAsync(b, "A");
            Assert.IsTrue(await WarteAsync(() => Abstimmung(a)?.Voted == 2));
            await Task.Delay(200);
            Assert.AreEqual(carlVorher, Zahl(c), "Carls Telefon bekam einen Stand, als Bert abstimmte.");
            Assert.AreEqual(0, Abstimmung(c)!.Voted);
        }

        /// <summary>Die Gegenprobe aus dem Entwurf: das Gegnertelefon verbindet mitten in der Runde neu - vor und nach dem Schluss.</summary>
        [TestMethod]
        public async Task AnOpponentPhoneReconnectingMidRoundLearnsNothingOfTheOtherTeam()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await VerbindeAsync(carl);
            await AbstimmungAsync();
            await StimmeAsync(a, "B");
            Assert.IsTrue(await WarteAsync(() => Abstimmung(b)?.Voted == 1));

            // --- Vor dem Schluss: Carl verbindet neu und liest nur Blau ---
            var c = await VerbindeAsync(carl);
            Assert.IsTrue(Abstimmung(c) is { Voted: 0, Of: 1, Closed: false, Result: "" }, "Carl liest nach dem Neuverbinden mehr als Blau.");
            Assert.AreEqual(0, Abstimmung(c)!.Own.Count);

            await StimmeAsync(c, "A");
            await StimmeAsync(b, "B");
            Assert.IsTrue(await WarteAsync(() => Abstimmung(a) is { Closed: true }), "Die Abstimmung schließt nicht, obwohl alle abgestimmt haben.");
            Assert.AreEqual("", Abstimmung(a)!.Result, "Vor der Auswertung steht eine Antwort am Telefon.");

            // --- Nach dem Schluss: jedes Team liest nur seine Antwort, auch nach dem Neuverbinden ---
            await Steuerung.SetTeamVoteResultsAsync(new Dictionary<Guid, string> { [rot.Id] = "B (2 von 2)", [blau.Id] = "A (1 von 1)" });
            Assert.IsTrue(await WarteAsync(() => Abstimmung(a)?.Result == "B (2 von 2)"), "Anna liest nicht die Antwort von Rot: " + Abstimmung(a)?.Result);
            Assert.IsTrue(await WarteAsync(() => Abstimmung(c)?.Result == "A (1 von 1)"), "Carl liest nicht die Antwort von Blau: " + Abstimmung(c)?.Result);

            var wieder = await VerbindeAsync(carl);
            Assert.IsTrue(Abstimmung(wieder) is { Result: "A (1 von 1)", Voted: 1, Of: 1, Closed: true }, "Carl liest nach dem Neuverbinden fremde Antworten: " + Abstimmung(wieder)?.Result);
            CollectionAssert.AreEqual(new[] { "A" }, Abstimmung(wieder)!.Own);
        }

        /// <summary>Wer abstimmt, sagt die Verbindung: eine fremde Kennung in der Nutzlast zählt für den Absender.</summary>
        [TestMethod]
        public async Task AVoteWithAnotherPlayersIdCountsForTheSenderOnly()
        {
            var a = await VerbindeAsync(anna);
            var c = await VerbindeAsync(carl);
            await AbstimmungAsync();

            await a.Verbindung.InvokeAsync("SelectionResults", new { selectedKeys = new[] { "C" }, playerId = carl.Id, teamId = blau.Id });

            Assert.IsTrue(await WarteAsync(() => Abstimmung(a)?.Voted == 1), "Annas Stimme zählt nicht.");
            Assert.IsFalse(Steuerung.StateManager.BuzzerKeySelector.KeyResultsForPlayer.ContainsKey(carl.Id), "Anna stimmte für Carl ab.");
            Assert.AreEqual(0, Abstimmung(c)!.Voted, "Annas Stimme zählt bei Blau.");
        }

        /// <summary>Ein per Joker gesperrtes Mitglied stimmt nicht ab und zählt nicht mit.</summary>
        [TestMethod]
        public async Task ABlockedMemberCannotVoteAndIsNotCounted()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await AbstimmungAsync(anna.Id, carl.Id);

            Assert.IsTrue(await WarteAsync(() => Abstimmung(a) is { Of: 1 }), "Das gesperrte Mitglied zählt mit: " + Abstimmung(a)?.Of);
            await StimmeAsync(b, "A");
            await Task.Delay(200);

            Assert.IsFalse(Steuerung.StateManager.BuzzerKeySelector.KeyResultsForPlayer.ContainsKey(bert.Id), "Das gesperrte Mitglied hat abgestimmt.");
            Assert.AreEqual(0, Abstimmung(a)!.Voted);
        }

        /// <summary>Jede Stimme geht an das ganze Team - mehr als fünf in zehn Sekunden nimmt der Hub nicht an.</summary>
        [TestMethod]
        public async Task MoreThanFiveVotesInTenSecondsAreDropped()
        {
            var a = await VerbindeAsync(anna);
            await VerbindeAsync(bert);
            var gemeldet = 0;
            Steuerung.EventBus.PlayerSelectedKeys += _ => Interlocked.Increment(ref gemeldet);
            await AbstimmungAsync();

            foreach (var taste in new[] { "A", "B", "A", "B", "A", "C", "C" })
                await StimmeAsync(a, taste);

            await Task.Delay(200);
            Assert.AreEqual(TeamVoteBoard.MaxVotesPerWindow, Volatile.Read(ref gemeldet), "Die Grenze greift nicht.");
            CollectionAssert.AreEqual(new[] { "A" }, Steuerung.StateManager.BuzzerKeySelector.KeyResultsForPlayer[anna.Id].SelectedKeys, "Eine Stimme über der Grenze zählt.");
        }

        /// <summary>M37 P6, R2-3: eine an der Grenze verworfene Stimme bekommt ihren Stand zurück - nur das eigene Telefon.</summary>
        [TestMethod]
        public async Task ADroppedVoteSendsTheCallerTheVoteThatCounts()
        {
            var a = await VerbindeAsync(anna);
            var b = await VerbindeAsync(bert);
            await AbstimmungAsync();

            foreach (var taste in new[] { "A", "B", "A", "B", "A" })
                await StimmeAsync(a, taste);

            Assert.IsTrue(await WarteAsync(() => Abstimmung(a)?.Own.SequenceEqual(["A"]) == true && Abstimmung(b)?.Voted == 1));
            await Task.Delay(200);
            var annaVorher = Zahl(a);
            var bertVorher = Zahl(b);

            await StimmeAsync(a, "C");

            Assert.IsTrue(await WarteAsync(() => Zahl(a) > annaVorher), "Die verworfene Stimme bekam keinen Stand - das Telefon zeigt eine Stimme, die nicht zählt.");
            CollectionAssert.AreEqual(new[] { "A" }, Abstimmung(a)!.Own, "Der Stand nach dem Verwerfen nennt nicht die Stimme, die zählt.");
            await Task.Delay(200);
            Assert.AreEqual(bertVorher, Zahl(b), "Die verworfene Stimme ging an das ganze Team.");
        }

        /// <summary>„Runde zurücksetzen" stimmt neu ab: die alte Antwort steht nicht mehr da; ohne Abstimmung zeigt das Telefon keine.</summary>
        [TestMethod]
        public async Task AResetDropsTheOldAnswerAndWithoutAVoteThereIsNone()
        {
            var a = await VerbindeAsync(anna);
            await AbstimmungAsync(anna.Id);
            await StimmeAsync(a, "B");
            await Steuerung.SetTeamVoteResultsAsync(new Dictionary<Guid, string> { [rot.Id] = "B (1 von 1)" });
            Assert.IsTrue(await WarteAsync(() => Abstimmung(a)?.Result == "B (1 von 1)"));

            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.KeySelect);
            Assert.IsTrue(await WarteAsync(() => Abstimmung(a) is { Closed: false, Voted: 0, Result: "" }), "Nach dem Zurücksetzen steht die alte Antwort da.");

            // Die neue Runde schließt: bis zur neuen Auswertung steht keine Antwort da - auch nicht die alte.
            await StimmeAsync(a, "A");
            Assert.IsTrue(await WarteAsync(() => Abstimmung(a) is { Closed: true }));
            Assert.AreEqual("", Abstimmung(a)!.Result, "Nach dem Zurücksetzen steht die Antwort der alten Runde wieder da.");

            Steuerung.EndTeamVote();
            await Steuerung.ResetRoundAsync(1, BuzzerControlsLayout.KeySelect);
            Assert.IsTrue(await WarteAsync(() => Letzter(a)?.Team is { Vote: null }), "Ohne Abstimmung zeigt das Telefon eine.");
        }
    }
}
