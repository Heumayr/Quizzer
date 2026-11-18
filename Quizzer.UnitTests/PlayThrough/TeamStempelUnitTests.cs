using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Der Stempel am Ergebnis (M37 T2, F-M37-07): beim Öffnen der Zelle bekommt jede neue Ergebniszeile das Team ihres
    /// Spielers, in der Datenbank. Wer danach umgesetzt wird, lässt diese Punkte beim alten Team (A08) - der Verlauf wird
    /// gemessen, nicht nur der Endzustand: erst stempeln, dann umsetzen, dann die Zelle erneut öffnen.
    /// </summary>
    [TestClass]
    public class TeamStempelUnitTests
    {
        private TestGameBuilder world = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(normalStepCount: 1, playerCount: 2);
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = world.Game.Id, Name = "Rot", ColorIndex = 5, SortOrder = 0 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = world.Game.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };

            using (var ctrl = new GameTeamsController())
            {
                await ctrl.InsertAsync(rot);
                await ctrl.InsertAsync(blau);
                await ctrl.SaveChangesAsync();
            }

            world.Game.IsTeamGame = true;
            world.Game.GameTeams.AddRange([rot, blau]);
            await SetzeAsync(world.Players[0], rot);
            await SetzeAsync(world.Players[1], blau);
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>Setzt einen Spieler in ein Team - im Speicher und in der Datenbank, wie „Teams umsetzen“.</summary>
        private async Task SetzeAsync(Player spieler, GameTeam team)
        {
            world.Game.PlayerXGames.Single(x => x.PlayerId == spieler.Id).GameTeamId = team.Id;

            await using var context = new Quizzer.Logic.Context.DataContext();
            context.PlayerXGames.Single(x => x.GameId == world.Game.Id && x.PlayerId == spieler.Id).GameTeamId = team.Id;
            await context.SaveChangesAsync();
        }

        /// <summary>Öffnet die Zelle und liefert den Stempel, den die Karte des Spielers trägt - den schreibt die Wertung.</summary>
        private async Task<Guid?> OeffneZelleAsync(Player spieler)
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            return vm.PlayersResultViewModel!.PlayerResultContextList.Single(k => k.Player.Id == spieler.Id).Result.GameTeamId;
        }

        private async Task<Dictionary<Guid, Guid?>> StempelAsync()
        {
            using var ctrl = new QuestionResultsController();
            var ergebnisse = await ctrl.GetAllResultsForCoordinate(world.Coordinate.Id) ?? [];
            return ergebnisse.ToDictionary(r => r.PlayerId, r => r.GameTeamId);
        }

        [TestMethod]
        public async Task OpeningTheCellStampsEveryResultWithTheTeamOfItsPlayer()
        {
            await OeffneZelleAsync(world.Players[0]);

            var stempel = await StempelAsync();

            Assert.AreEqual(2, stempel.Count, "Beim Öffnen sind nicht für beide Spieler Ergebniszeilen entstanden.");
            Assert.AreEqual(rot.Id, stempel[world.Players[0].Id], "Spieler 1 trägt nicht den Stempel seines Teams.");
            Assert.AreEqual(blau.Id, stempel[world.Players[1].Id], "Spieler 2 trägt nicht den Stempel seines Teams.");
        }

        /// <summary>F-M37-07: gebucht, umgesetzt, die Zelle erneut geöffnet - der alte Stempel bleibt, die Punkte bleiben bei Rot.</summary>
        [TestMethod]
        public async Task MovingAPlayerAfterABookingLeavesTheOldStamp()
        {
            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await vm.LoadForTestAsync();
            vm.PlayersResultViewModel!.PlayerResultContextList.Single(k => k.Player.Id == world.Players[0].Id).Suggestion =
                Quizzer.Views.GameViews.Sub.PlayerResultContext.ScoreSuggestion.Right;
            Assert.IsTrue(await vm.PlayersResultViewModel.TrySaveAsync(), "Das Buchen scheiterte.");
            Assert.AreEqual(rot.Id, (await StempelAsync())[world.Players[0].Id], "Vor dem Umsetzen fehlt der Stempel - dann misst der Test nichts.");

            await SetzeAsync(world.Players[0], blau);
            var aufDerKarte = await OeffneZelleAsync(world.Players[0]);

            Assert.AreEqual(rot.Id, (await StempelAsync())[world.Players[0].Id], "Das Umsetzen hat den Stempel einer gebuchten Zeile in der Datenbank überschrieben.");
            Assert.AreEqual(rot.Id, aufDerKarte, "Die Karte trägt nach dem Umsetzen das neue Team - die Wertung schriebe es fest, die Punkte wanderten mit.");
        }

        /// <summary>R4-3: nur angesehen (keine Buchung), umgesetzt, erneut geöffnet - die Zeile bekommt den Stempel des neuen Teams.</summary>
        [TestMethod]
        public async Task MovingAPlayerAfterOnlyViewingGivesTheNewStamp()
        {
            await OeffneZelleAsync(world.Players[0]);
            Assert.AreEqual(rot.Id, (await StempelAsync())[world.Players[0].Id], "Vor dem Umsetzen fehlt der Stempel - dann misst der Test nichts.");

            await SetzeAsync(world.Players[0], blau);
            var aufDerKarte = await OeffneZelleAsync(world.Players[0]);

            Assert.AreEqual(blau.Id, (await StempelAsync())[world.Players[0].Id], "Die leere Zeile behält in der Datenbank den alten Stempel.");
            Assert.AreEqual(blau.Id, aufDerKarte, "Die Karte trägt ohne Buchung das alte Team.");
            Assert.AreEqual(blau.Id, (await StempelAsync())[world.Players[1].Id], "Der nicht umgesetzte Spieler hat seinen Stempel verloren.");
        }

        /// <summary>Ohne Teamspiel bleibt der Stempel leer, auch wenn Teams und Zuteilung noch dastehen.</summary>
        [TestMethod]
        public async Task WithoutTeamGameThereIsNoStamp()
        {
            world.Game.IsTeamGame = false;

            await OeffneZelleAsync(world.Players[0]);

            Assert.IsTrue((await StempelAsync()).Values.All(v => v == null), "Ohne Teamspiel wurde gestempelt.");
        }
    }
}
