using LocalBuzzer.Service.Base;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.TestShared;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using PrePhase = LocalBuzzer.Service.Base.States.Joker.MinigamePrePhase;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Die Teamberatung im Fragefenster (M37 T5), echter Server und Telefone: nur die Sprecher antworten - per Text, wo die
    /// Frage Tasten hat per Tastenwahl -, die anderen lesen, mit wem sie beraten, und jedes Telefon nennt den Sprecher. Wer per
    /// Joker gesperrt oder getrennt ist, spricht nicht; der Nächste übernimmt. Die Wertung steht in <c>.Wertung.cs</c>, der
    /// Sprecherstand in <c>.Sprecher.cs</c>.
    /// </summary>
    [TestClass]
    public partial class TeamberatungUnitTests : SonderregelServerTestBasis
    {
        protected override int Port => TestPorts.Teamberatung;

        private GameTeam rot = null!;
        private GameTeam blau = null!;

        /// <summary>Welche Plätze Rot sind - die übrigen sind Blau.</summary>
        private int[] rotePlaetze = [0, 1];

        private int beratungszeit;

        private bool teamspiel = true;

        private bool unterNullErlaubt = true;

        private (int Spieler, JokerKind Art)? jokerFuer;

        private PlayerJoker? joker;

        /// <summary>Zwei Teams in der Datenbank, die Plätze zugeteilt - das Öffnen der Zelle stempelt -, dazu ein Joker, wenn gewünscht.</summary>
        private async Task TeamsAnlegenAsync(TestGameBuilder welt)
        {
            welt.Game.AllowNegativeScore = unterNullErlaubt;

            if (!teamspiel)
                return;

            rot = new GameTeam { Id = Guid.NewGuid(), GameId = welt.Game.Id, Name = "Rot", ColorIndex = 5, SortOrder = 0 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = welt.Game.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };

            using (var ctrl = new GameTeamsController())
            {
                await ctrl.InsertAsync(rot);
                await ctrl.InsertAsync(blau);
                await ctrl.SaveChangesAsync();
            }

            welt.Game.IsTeamGame = true;
            welt.Game.TeamCouncilSeconds = beratungszeit;
            welt.Game.GameTeams.AddRange([rot, blau]);

            await using var context = new Quizzer.Logic.Context.DataContext();

            for (var i = 0; i < welt.Players.Count; i++)
            {
                var team = rotePlaetze.Contains(i) ? rot : blau;
                welt.Game.PlayerXGames.Single(x => x.PlayerId == welt.Players[i].Id).GameTeamId = team.Id;
                context.PlayerXGames.Single(x => x.GameId == welt.Game.Id && x.PlayerId == welt.Players[i].Id).GameTeamId = team.Id;
            }

            await context.SaveChangesAsync();

            if (jokerFuer is { } wer)
            {
                using var ctrl = new PlayerJokersController();
                joker = await ctrl.GrantAsync(welt.Game.Id, welt.Players[wer.Spieler].Id, wer.Art, "verdient", DateTimeOffset.Now);
                await ctrl.SaveChangesAsync();
            }
        }

        private Task<TestableCurrentQuestionViewModel> OeffneTeamberatungAsync(
            int spieler = 4, int[]? kontostaende = null, QuestionType typ = QuestionType.Default, CellSpecialRule regel = CellSpecialRule.TeamCouncil)
        {
            VorDemOeffnen = TeamsAnlegenAsync;
            return OeffneAsync(regel, spieler, kontostaende, typ: typ);
        }

        private LayoutStateManager Hub => serverVm!.BuzzerControlsViewModel!.BuzzerController!.StateManager;

        private Guid Id(int spieler) => world!.Players[spieler].Id;

        private string Name(int spieler) => world!.Players[spieler].CalculatedDisplayName;

        private string BeratSatz(int sprecher) => $"Berate dich mit {Name(sprecher)} – {Name(sprecher)} gibt eure Antwort ab.";

        private Task JokerAsync(int telefon, Guid? jokerId, Guid? ziel = null)
            => phones[telefon].InvokeAsync("SpielAktion", new
            {
                kind = 0,
                specialRule = 0,
                prePhase = (int)PrePhase.Joker,
                durchgang = Zustand.Durchgang,
                payload = new { jokerId, target = ziel },
            });

        /// <summary>Die Joker-Runde ist zu, bevor die Sprecher stehen - gewartet wird auf die Frage an den Telefonen.</summary>
        private async Task NachDerJokerRundeAsync(TestableCurrentQuestionViewModel vm)
        {
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Die Joker-Runde schließt nicht.");
            Assert.IsTrue(await WarteAsync(() => Hub.CurrentLayout == BuzzerControlsLayout.Input && Hub.AnswerLock.Answerers?.Count == 2),
                "Nach der Joker-Runde stehen die Sprecher nicht fest.");
        }

        [TestMethod]
        public async Task OnlyTheSpeakersAnswerTheOthersConsultAndEveryPhoneNamesTheSpeaker()
        {
            var vm = await OeffneTeamberatungAsync();

            Assert.AreEqual(BuzzerControlsLayout.Input, vm.CurrentBuzzerLayout, "Der Sprecher bekommt keine Texteingabe.");
            Assert.IsTrue(await WarteAsync(() => Hub.CurrentLayout == BuzzerControlsLayout.Input), "Die Telefone zeigen keine Eingabe.");
            Assert.AreEqual(CurrentQuestionViewModel.TeamregelEingabe, Hub.BuzzerInputState.Infos.Placeholder);
            CollectionAssert.AreEquivalent(new[] { Id(0), Id(2) }, Hub.AnswerLock.Answerers!.ToArray(), "Nicht genau die Sprecher dürfen antworten.");

            Assert.IsTrue(await WarteAsync(() => Stand(1)?.ExcludedReason == BeratSatz(0)), "Bert liest nicht, mit wem er berät: " + Stand(1)?.ExcludedReason);
            Assert.IsTrue(Stand(1)!.CurrentLayoutLocked, "Das Telefon des Nicht-Sprechers ist offen.");
            Assert.IsTrue(Stand(0) is { ExcludedFromQuestion: false, CurrentLayoutLocked: false }, "Der Sprecher kann nicht eingeben.");
            CollectionAssert.AreEqual(new[] { Name(0), Name(0), Name(2), Name(2) }, staende.Select((_, i) => Stand(i)?.Team?.Speaker).ToArray(),
                "Nicht jedes Telefon nennt den Sprecher seines Teams.");

            Assert.AreEqual("Teamregel: Teamberatung", vm.Sonderregel.Titel);
            Assert.AreEqual($"{SpecialRuleProfiles.Symbol} Sprecher: ✚ Rot – {Name(0)} · ★ Blau – {Name(2)}", vm.Sonderregel.Hinweis);
            Assert.AreEqual("Sprecher für Rot", Karte(vm, 0).SonderregelText);
            Assert.AreEqual($"Berät mit {Name(0)}", Karte(vm, 1).SonderregelText);

            // --- Der Nicht-Sprecher tippt: verworfen. Der Sprecher tippt: seine Antwort steht auf seiner Karte. ---
            await phones[1].InvokeAsync("SubmitInput", new { value = "Wien" });
            await phones[0].InvokeAsync("SubmitInput", new { value = "Paris" });
            Assert.IsTrue(await WarteAsync(() => Karte(vm, 0).InputResult?.Value == "Paris"), "Die Antwort des Sprechers kam nicht auf seine Karte.");
            Assert.IsNull(Karte(vm, 1).InputResult, "Die Antwort des Nicht-Sprechers kam an.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        /// <summary>Eine Frage mit Tasten: der Sprecher wählt, statt zu tippen.</summary>
        [TestMethod]
        public async Task AQuestionWithKeysLetsTheSpeakerChooseAKey()
        {
            var vm = await OeffneTeamberatungAsync(typ: QuestionType.MultipleChoice);

            Assert.AreEqual(BuzzerControlsLayout.KeySelect, vm.CurrentBuzzerLayout);
            Assert.IsTrue(await WarteAsync(() => Hub.CurrentLayout == BuzzerControlsLayout.KeySelect));
            CollectionAssert.AreEquivalent(new[] { Id(0), Id(2) }, Hub.AnswerLock.Answerers!.ToArray());
        }

        /// <summary>Per Joker gesperrt: der Nächste spricht - am Hub, am Telefon und auf der Karte; die Spielleitung hebt es auf.</summary>
        [TestMethod]
        public async Task ASpeakerBlockedByAJokerPassesToTheNextUntilTheMasterLiftsIt()
        {
            jokerFuer = (2, JokerKind.Block);
            var vm = await OeffneTeamberatungAsync();
            Assert.IsTrue(vm.VorphaseLaeuft, "Vor der Teamberatung lief keine Joker-Runde.");

            await JokerAsync(2, joker!.Id, ziel: Id(0));
            await NachDerJokerRundeAsync(vm);
            CollectionAssert.AreEquivalent(new[] { Id(1), Id(2) }, Hub.AnswerLock.Answerers!.ToArray(), "Der gesperrte Sprecher darf antworten.");
            Assert.IsTrue(await WarteAsync(() => Stand(1)?.Team?.Speaker == Name(1)), "Das Telefon nennt den gesperrten Sprecher: " + Stand(1)?.Team?.Speaker
                + " / Hub: " + Hub.Teams.TeamOf(Id(1))?.Speaker);
            Assert.IsTrue(await WarteAsync(() => Stand(0)?.ExcludedReason?.Contains("per Joker gesperrt") == true),
                "Der gesperrte Sprecher liest nicht, wer ihn gesperrt hat: " + Stand(0)?.ExcludedReason);
            Assert.AreEqual("Sprecher für Rot", Karte(vm, 1).SonderregelText);
            Assert.AreEqual($"Berät mit {Name(1)} · Gesperrt per Joker von {Name(2)}", Karte(vm, 0).SonderregelText, "Die Karte verschweigt die Joker-Sperre.");
            StringAssert.Contains(vm.Sonderregel.Hinweis, $"Rot – {Name(1)}");
            StringAssert.Contains(vm.Sonderregel.Hinweis, "Joker", "Der Beamer verliert den Joker aus dem Hinweis.");

            // Es zählt die Wertung des Sprechers dieser Frage, nicht die des Ersten der Folge.
            Karte(vm, 0).Suggestion = Quizzer.Views.GameViews.Sub.PlayerResultContext.ScoreSuggestion.Wrong;
            Karte(vm, 1).Suggestion = Quizzer.Views.GameViews.Sub.PlayerResultContext.ScoreSuggestion.Right;
            Assert.AreEqual(world!.Coordinate.CurrentPoints, Karte(vm, 0).CurrentScoreManipulation + Karte(vm, 1).CurrentScoreManipulation,
                "Für Rot zählt die Wertung des Gesperrten statt die des Sprechers.");

            await vm.HebeJokerSperreAufAsync();

            CollectionAssert.AreEquivalent(new[] { Id(0), Id(2) }, Hub.AnswerLock.Answerers!.ToArray(), "Nach dem Aufheben spricht der Erste nicht wieder.");
            Assert.IsTrue(await WarteAsync(() => Stand(1)?.Team?.Speaker == Name(0)));
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }

        [TestMethod]
        public async Task ADisconnectedSpeakerPassesToTheNext()
        {
            FensterEinrichten = async _ =>
            {
                await phones[0].DisposeAsync();
                Assert.IsTrue(await WarteAsync(() => world!.Players[0].ConnectionState == PlayerConnection.Disconnected), "Das Telefon trennt nicht.");
            };

            var vm = await OeffneTeamberatungAsync();

            CollectionAssert.AreEquivalent(new[] { Id(1), Id(2) }, Hub.AnswerLock.Answerers!.ToArray(), "Ein getrennter Sprecher spricht.");
            Assert.IsTrue(await WarteAsync(() => Stand(1)?.Team?.Speaker == Name(1)));

            // Der Sprecher dieser Frage galt nur ihr: danach nennt das Telefon wieder den des Stands.
            await vm.ClosedForTestAsync();
            Assert.IsTrue(await WarteAsync(() => Stand(1)?.Team?.Speaker == Name(0)), "Nach der Frage bleibt der Sprecher dieser Frage stehen.");
        }

        /// <summary>Die Beratungszeit des Spiels läuft von selbst; ist sie um, nehmen die Telefone nichts mehr an.</summary>
        [TestMethod]
        public async Task TheCouncilTimeRunsByItselfAndLocksThePhones()
        {
            beratungszeit = 1;
            FensterEinrichten = vm =>
            {
                vm.GameMasterViewModel = new GameMasterViewModel { Game = world!.Game };
                return Task.CompletedTask;
            };

            var vm = await OeffneTeamberatungAsync();

            Assert.IsTrue(vm.Countdown!.Laeuft, "Die Beratungszeit läuft nicht.");
            StringAssert.Contains(vm.Sonderregel.Stand, "1 s");

            // Ohne Dispatcher-Schleife tickt die Uhr nicht von selbst - der Test treibt sie.
            vm.Countdown.Weiter(DateTime.UtcNow.AddSeconds(0.5));
            Assert.IsTrue(vm.Countdown.Laeuft, "Die Beratungszeit ist kürzer als die des Spiels.");
            Assert.IsFalse(Hub.ZeitAbgelaufen);

            vm.Countdown.Weiter(DateTime.UtcNow.AddSeconds(1.5));
            Assert.IsTrue(await WarteAsync(() => Hub.ZeitAbgelaufen), "Nach der Beratungszeit nehmen die Telefone weiter an.");
        }

        [TestMethod]
        public async Task WithoutCouncilTimeThereIsNoCountdown()
        {
            FensterEinrichten = vm =>
            {
                vm.GameMasterViewModel = new GameMasterViewModel { Game = world!.Game };
                return Task.CompletedTask;
            };

            var vm = await OeffneTeamberatungAsync();

            Assert.IsFalse(vm.Countdown!.Laeuft, "Ohne Beratungszeit läuft ein Countdown.");
        }

        /// <summary>Fail-closed (Abschnitt 7): eine Auktion im Teamspiel spielt ohne Vorphase, eine Teamberatung ohne Teams ohne Sprecher.</summary>
        [TestMethod]
        public async Task RulesThatDoNotFitThePlayAreNotPlayed()
        {
            var auktion = await OeffneTeamberatungAsync(regel: CellSpecialRule.Auction);

            Assert.IsFalse(auktion.VorphaseLaeuft, "Die Auktion spielt im Teamspiel ihre Vorphase.");
            Assert.AreEqual(BuzzerControlsLayout.Buzzer, auktion.CurrentBuzzerLayout);
            Assert.IsFalse(Hub.AnswerLock.Active, "Die Auktion sperrt im Teamspiel Antwortende.");
        }

        [TestMethod]
        public async Task WithoutTeamsTheCouncilIsNotPlayed()
        {
            teamspiel = false;
            var vm = await OeffneTeamberatungAsync();

            Assert.AreEqual(BuzzerControlsLayout.Buzzer, vm.CurrentBuzzerLayout, "Ohne Teams wird die Frage zur Texteingabe.");
            Assert.IsFalse(Hub.AnswerLock.Active, "Ohne Teams antworten nur einige.");
            Assert.AreEqual(string.Empty, Karte(vm, 1).SonderregelText);
        }
    }
}
