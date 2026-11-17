using LocalBuzzer.Service.Base;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Das Finale im Teamspiel (M37 E4), echter Server und Telefone: jedes Mitglied setzt verdeckt von seinem eigenen Kontostand,
    /// dann antwortet nur der Sprecher fürs Team - die Abgabe eines anderen wird verworfen. Die Bewertung des Sprechers gilt
    /// fürs ganze Team, je Mitglied auf den eigenen Einsatz und gekappt am eigenen Kontostand. Aufgelöst wird Team für Team,
    /// aufsteigend nach Teamstand.
    /// </summary>
    [TestClass]
    public class FinaleTeamUnitTests : SonderregelServerTestBasis
    {
        protected override int Port => TestPorts.FinaleTeam;

        private GameTeam rot = null!;
        private GameTeam blau = null!;

        private bool mitJoker;

        /// <summary>Rot sind die Plätze 0 und 1, Blau 2 und 3; die Plätze zugeteilt - das Öffnen der Zelle stempelt.</summary>
        private async Task TeamsAnlegenAsync(TestGameBuilder welt)
        {
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = welt.Game.Id, Name = "Rot", ColorIndex = 5, SortOrder = 0 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = welt.Game.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };

            using (var ctrl = new GameTeamsController())
            {
                await ctrl.InsertAsync(rot);
                await ctrl.InsertAsync(blau);
                await ctrl.SaveChangesAsync();
            }

            welt.Game.IsTeamGame = true;
            welt.Game.GameTeams.AddRange([rot, blau]);

            await using var context = new Quizzer.Logic.Context.DataContext();

            for (var i = 0; i < welt.Players.Count; i++)
            {
                var team = i < 2 ? rot : blau;
                welt.Game.PlayerXGames.Single(x => x.PlayerId == welt.Players[i].Id).GameTeamId = team.Id;
                context.PlayerXGames.Single(x => x.GameId == welt.Game.Id && x.PlayerId == welt.Players[i].Id).GameTeamId = team.Id;
            }

            await context.SaveChangesAsync();

            if (mitJoker)
            {
                using var ctrl = new PlayerJokersController();
                await ctrl.GrantAsync(welt.Game.Id, welt.Players[3].Id, JokerKind.Double, "verdient", DateTimeOffset.Now);
                await ctrl.SaveChangesAsync();
            }
        }

        private LayoutStateManager Hub => serverVm!.BuzzerControlsViewModel!.BuzzerController!.StateManager;

        private Guid Id(int spieler) => world!.Players[spieler].Id;

        private string Name(int spieler) => world!.Players[spieler].CalculatedDisplayName;

        /// <summary>
        /// Anna 300, Bert 30, Carl 50, Dora 50 - Rot steht bei 330, Blau bei 100. Anna setzt 200, Bert 100 (seine Grenze sind die
        /// Feldpunkte), Carl 50, Dora nichts; dann ist die Vorphase zu.
        /// </summary>
        private async Task<TestableCurrentQuestionViewModel> NachDenEinsaetzenAsync()
        {
            VorDemOeffnen = TeamsAnlegenAsync;
            var vm = await OeffneAsync(CellSpecialRule.Final, spieler: 4, kontostaende: [300, 30, 50, 50]);

            Assert.IsTrue(vm.VorphaseLaeuft, "Im Teamspiel setzt beim Finale niemand.");
            Assert.AreEqual("Sonderregel: Finale", vm.Sonderregel.Titel);

            await AbgebenAsync(0, CellSpecialRule.Final, new { amount = 200 });
            await AbgebenAsync(1, CellSpecialRule.Final, new { amount = 100 });
            await AbgebenAsync(2, CellSpecialRule.Final, new { amount = 50 });
            Assert.IsTrue(await WarteAsync(() => Zustand.Rules?.CurrentSubmissions.Count == 3), "Nicht jedes Mitglied konnte setzen.");

            await vm.SchliesseVorphaseAsync();
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Die Vorphase lässt sich nicht schließen.");
            Assert.IsTrue(await WarteAsync(() => Hub.CurrentLayout == BuzzerControlsLayout.Input && Hub.AnswerLock.Active),
                "Nach den Einsätzen antworten nicht die Sprecher.");
            return vm;
        }

        [TestMethod]
        public async Task OnlyTheSpeakerAnswersForTheTeamAndANonSpeakerIsDiscarded()
        {
            var vm = await NachDenEinsaetzenAsync();

            CollectionAssert.AreEquivalent(new[] { Id(0), Id(2) }, Hub.AnswerLock.Answerers!.ToArray(), "Nicht genau die Sprecher dürfen antworten.");
            Assert.AreEqual(CurrentQuestionViewModel.TeamregelEingabe, Hub.BuzzerInputState.Infos.Placeholder);
            Assert.IsTrue(Hub.BuzzerInputState.EinmalAbgabe, "Der Sprecher darf die Antwort seines Teams ändern.");
            Assert.AreEqual("Sprecher für Rot · Einsatz: 200", Karte(vm, 0).SonderregelText);
            Assert.AreEqual($"Berät mit {Name(0)} · Einsatz: 100", Karte(vm, 1).SonderregelText);
            Assert.AreEqual($"Berät mit {Name(2)} · Kein Einsatz – setzt 0", Karte(vm, 3).SonderregelText);

            // --- Bert ist nicht Sprecher: verworfen, nichts auf seiner Karte, nichts am Hub ---
            await phones[1].InvokeAsync("SubmitInput", new { value = "Wien" });
            await phones[0].InvokeAsync("SubmitInput", new { value = "Ottawa" });
            Assert.IsTrue(await WarteAsync(() => Karte(vm, 0).InputResult?.Value == "Ottawa"), "Die Antwort des Sprechers kam nicht an.");
            Assert.IsNull(Karte(vm, 1).InputResult, "Die Antwort des Nicht-Sprechers kam an.");
            Assert.IsFalse(Hub.BuzzerInputState.InputsForPlayer.ContainsKey(Id(1)), "Die Antwort des Nicht-Sprechers steht am Hub.");

            // Mit beiden Sprechern ist die Runde zu - die Nicht-Sprecher halten sie nicht offen.
            await phones[2].InvokeAsync("SubmitInput", new { value = "Toronto" });
            Assert.IsTrue(await WarteAsync(() => vm.FinaleAufloesenVisibility == Visibility.Visible), "Nach beiden Sprechern lässt sich nicht auflösen.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        /// <summary>Die Bewertung des Sprechers breitet sich aus: je Mitglied der eigene Einsatz, gekappt am eigenen Kontostand (E2).</summary>
        [TestMethod]
        public async Task TheSpeakersRatingCountsForEachMemberOnTheOwnStake()
        {
            var vm = await NachDenEinsaetzenAsync();

            Karte(vm, 0).Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(200, Karte(vm, 0).CurrentScoreManipulation, "Der Sprecher bekommt nicht seinen Einsatz.");
            Assert.AreEqual(100, Karte(vm, 1).CurrentScoreManipulation, "Bert bekommt nicht seinen eigenen Einsatz.");
            StringAssert.Contains(Karte(vm, 1).SuggestionText, "Finale Rot: Einsatz 100");

            Karte(vm, 2).Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-50, Karte(vm, 2).CurrentScoreManipulation);
            Assert.AreEqual(0, Karte(vm, 3).CurrentScoreManipulation, "Ohne Einsatz verliert Dora etwas.");
            Assert.AreEqual(200, Karte(vm, 0).CurrentScoreManipulation, "Blaus Wertung trifft Rot.");

            // E2: Rot liegt falsch, ohne Minus im Spiel - Bert verliert höchstens seine 30.
            Karte(vm, 0).Coordinate!.Game.AllowNegativeScore = false;
            Karte(vm, 0).Suggestion = ScoreSuggestion.Wrong;
            Assert.AreEqual(-200, Karte(vm, 0).CurrentScoreManipulation);
            Assert.AreEqual(-30, Karte(vm, 1).CurrentScoreManipulation, "Berts verlorener Einsatz ist nicht gekappt.");
        }

        /// <summary>Team für Team, aufsteigend nach Teamstand; je Team die Antwort des Sprechers und die Einsätze aller.</summary>
        [TestMethod]
        public async Task TheBeamerRevealsTeamByTeamFromTheLowestTeamTotal()
        {
            var vm = await NachDenEinsaetzenAsync();
            await phones[0].InvokeAsync("SubmitInput", new { value = "Ottawa" });
            await phones[2].InvokeAsync("SubmitInput", new { value = "Toronto" });
            Assert.IsTrue(await WarteAsync(() => vm.FinaleAufloesenVisibility == Visibility.Visible));
            StringAssert.Contains(vm.Sonderregel.Stand, "als Nächstes ★ Blau");

            Assert.IsTrue(vm.LoeseFinaleWeiterAuf());
            Assert.IsTrue(vm.LoeseFinaleWeiterAuf());

            CollectionAssert.AreEqual(new[]
            {
                $"★ Blau – Stand 100 · „Toronto“ · Einsätze: {Name(2)} 50, {Name(3)} 0",
                $"✚ Rot – Stand 330 · „Ottawa“ · Einsätze: {Name(0)} 200, {Name(1)} 100",
            }, vm.Sonderregel.Aufloesung.ToArray());
            Assert.IsFalse(vm.LoeseFinaleWeiterAuf());
        }

        /// <summary>Das Finale ist keine Teamregel: auch im Teamspiel läuft davor keine Joker-Runde, obwohl Dora einen Joker hält.</summary>
        [TestMethod]
        public async Task TheFinalInATeamGamePlaysNoJokerRound()
        {
            mitJoker = true;
            var vm = await NachDenEinsaetzenAsync();

            Assert.IsFalse(vm.VorphaseLaeuft, "Nach den Einsätzen läuft eine Joker-Runde.");
            Assert.AreEqual(BuzzerControlsLayout.Input, vm.CurrentBuzzerLayout);
            Assert.AreEqual(BuzzerControlsLayout.Input, Hub.CurrentLayout);
        }
    }
}
