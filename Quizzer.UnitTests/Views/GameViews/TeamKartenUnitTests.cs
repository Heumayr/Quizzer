using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using System.Windows.Controls;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Die Spielerkarten im Teamspiel (M37 T3): nach Team gruppiert, je Gruppe eine Kopfzeile mit dem, was die gesetzten
    /// Bewertungen dem Team bringen. Eine Karte steht beim Team ihres Stempels (F-M37-07), ohne Stempel beim heutigen,
    /// ohne Team unter „Ohne Team". Strg+1 … 9 zählt in der Reihenfolge, in der die Karten dastehen.
    /// </summary>
    [TestClass]
    public class TeamKartenUnitTests
    {
        private TestGameBuilder world = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(normalStepCount: 1, playerCount: 3);
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
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await world.DisposeAsync();
            UserPrompt.Reset();
        }

        /// <summary>Setzt einen Spieler in ein Team - im Speicher und in der Datenbank, wie „Teams umsetzen“.</summary>
        private async Task SetzeAsync(Player spieler, GameTeam? team)
        {
            world.Game.PlayerXGames.Single(x => x.PlayerId == spieler.Id).GameTeamId = team?.Id;

            await using var context = new Quizzer.Logic.Context.DataContext();
            context.PlayerXGames.Single(x => x.GameId == world.Game.Id && x.PlayerId == spieler.Id).GameTeamId = team?.Id;
            await context.SaveChangesAsync();
        }

        private async Task<PlayersResultViewModel> OeffneZelleAsync()
        {
            var frage = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };
            await frage.LoadForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();
            return frage.PlayersResultViewModel!;
        }

        private static string[] Namen(TeamKartengruppe gruppe) => gruppe.Karten.Select(k => k.Player.Id.ToString()).ToArray();

        [TestMethod]
        public async Task TheCardsAreGroupedByTeamInTheOrderOfTheSetup()
        {
            await SetzeAsync(world.Players[0], blau);
            await SetzeAsync(world.Players[1], rot);
            await SetzeAsync(world.Players[2], blau);

            var vm = await OeffneZelleAsync();

            CollectionAssert.AreEqual(new[] { "Rot", "Blau" }, vm.TeamGruppen.Select(g => g.Name).ToArray());
            CollectionAssert.AreEqual(new[] { world.Players[1].Id.ToString() }, Namen(vm.TeamGruppen[0]));
            CollectionAssert.AreEquivalent(new[] { world.Players[0].Id.ToString(), world.Players[2].Id.ToString() }, Namen(vm.TeamGruppen[1]));
            CollectionAssert.AreEqual(vm.TeamGruppen.SelectMany(g => g.Karten).ToArray(), vm.PlayerResultContextList.ToArray(),
                "Strg+1 … 9 zählt anders, als die Karten dastehen.");
            Assert.AreEqual(Visibility.Collapsed, vm.EinzelkartenVisibility);
        }

        /// <summary>Die Kopfzeile rechnet mit, sobald eine Bewertung gesetzt ist: „+200 (je +100)“.</summary>
        [TestMethod]
        public async Task TheHeaderAddsTheProposalsOfItsTeam()
        {
            await SetzeAsync(world.Players[0], rot);
            await SetzeAsync(world.Players[1], rot);
            await SetzeAsync(world.Players[2], blau);
            var vm = await OeffneZelleAsync();
            var rotGruppe = vm.TeamGruppen.Single(g => g.Team == rot);
            var gemeldet = new List<string?>();
            rotGruppe.PropertyChanged += (_, e) => gemeldet.Add(e.PropertyName);

            Assert.AreEqual(string.Empty, rotGruppe.VorschlagText, "Ohne Bewertung steht schon eine Zahl in der Kopfzeile.");

            rotGruppe.Karten[0].Suggestion = PlayerResultContext.ScoreSuggestion.Right;
            var punkte = rotGruppe.Karten[0].CurrentScoreManipulation;
            Assert.IsTrue(punkte > 0, "„Richtig“ bringt an dieser Zelle nichts - dann misst der Test nichts.");
            Assert.AreEqual($": +{punkte}", rotGruppe.VorschlagText);

            rotGruppe.Karten[1].Suggestion = PlayerResultContext.ScoreSuggestion.Right;
            Assert.AreEqual($": +{2 * punkte} (je +{punkte})", rotGruppe.VorschlagText);
            CollectionAssert.Contains(gemeldet, nameof(TeamKartengruppe.VorschlagText), "Die Kopfzeile erfährt nicht, dass sich eine Bewertung geändert hat.");
            Assert.AreEqual(string.Empty, vm.TeamGruppen.Single(g => g.Team == blau).VorschlagText, "Die Bewertung bei Rot steht in der Kopfzeile von Blau.");
        }

        /// <summary>
        /// F-M37-07 im Bewertungsfenster: wer nach dem Öffnen umgesetzt wurde, steht beim Team seines Stempels - dort zählt,
        /// was jetzt gebucht wird. Der Verlauf: öffnen (Stempel Rot), umsetzen, erneut öffnen.
        /// </summary>
        [TestMethod]
        public async Task AMovedPlayerIsRatedForTheTeamOfHisStamp()
        {
            await SetzeAsync(world.Players[0], rot);
            await SetzeAsync(world.Players[1], blau);
            await SetzeAsync(world.Players[2], blau);
            await OeffneZelleAsync();

            await SetzeAsync(world.Players[0], blau);
            var vm = await OeffneZelleAsync();

            CollectionAssert.AreEqual(new[] { world.Players[0].Id.ToString() }, Namen(vm.TeamGruppen.Single(g => g.Team == rot)),
                "Die Karte steht beim heutigen Team - die Kopfzeile verspräche Blau Punkte, die bei Rot zählen.");
        }

        /// <summary>Ohne Team (Beitritt am Telefon, noch nicht zugeteilt): eigene Gruppe mit dem Satz, dass es nicht zählt.</summary>
        [TestMethod]
        public async Task WithoutTeamTheCardSitsUnderOhneTeam()
        {
            await SetzeAsync(world.Players[0], rot);
            await SetzeAsync(world.Players[1], blau);

            var vm = await OeffneZelleAsync();
            var ohne = vm.TeamGruppen.Last();

            Assert.IsTrue(ohne.IstOhneTeam, "Die Karte ohne Team fehlt oder steht bei einem Team.");
            CollectionAssert.AreEqual(new[] { world.Players[2].Id.ToString() }, Namen(ohne));
            Assert.AreEqual($": {TeamKachel.OhneTeamText}", ohne.VorschlagText);
        }

        /// <summary>Gegenrichtung: ohne Teamspiel keine Gruppen, die Karten stehen wie bisher.</summary>
        [TestMethod]
        public async Task WithoutTeamGameThereAreNoGroups()
        {
            await SetzeAsync(world.Players[0], rot);
            world.Game.IsTeamGame = false;

            var vm = await OeffneZelleAsync();

            Assert.AreEqual(0, vm.TeamGruppen.Count);
            Assert.AreEqual(Visibility.Visible, vm.EinzelkartenVisibility);
            Assert.AreEqual(3, vm.PlayerResultContextList.Count);
        }

        /// <summary>Das Fenster selbst: Kopfzeilen mit Team und Vorschlag, alle Karten da, keine tote Bindung.</summary>
        [TestMethod]
        public async Task TheWindowShowsTheHeadersAndAllCards()
        {
            await SetzeAsync(world.Players[0], rot);
            await SetzeAsync(world.Players[1], blau);
            var vm = await OeffneZelleAsync();
            vm.TeamGruppen[0].Karten[0].Suggestion = PlayerResultContext.ScoreSuggestion.Right;

            UiTestHost.Run(() =>
            {
                using var wache = new BindingErrorWatch();
                var fenster = new PlayersResultView { DataContext = vm };
                var inhalt = (FrameworkElement)fenster.Content;
                inhalt.Measure(new Size(1100, 700));
                inhalt.Arrange(new Rect(0, 0, 1100, 700));
                inhalt.UpdateLayout();

                var texte = TeamBeamerUnitTests.Descendants<TextBlock>(inhalt).Where(TeamBeamerUnitTests.Sichtbar).Select(t => t.Text).ToList();

                Assert.AreEqual(3, TeamBeamerUnitTests.Descendants<UcPlayerResultView>(inhalt).Count(TeamBeamerUnitTests.Sichtbar), "Es fehlt eine Spielerkarte.");
                CollectionAssert.IsSubsetOf(new[] { "Rot", "✚", vm.TeamGruppen[0].VorschlagText, "Blau", "Ohne Team", $": {TeamKachel.OhneTeamText}" }, texte,
                    string.Join(" | ", texte.Where(t => !string.IsNullOrWhiteSpace(t)).Take(40)));
                Assert.AreEqual(0, wache.Errors.Count, "Bindungen der Teamgruppen greifen ins Leere: " + string.Join(" || ", wache.Errors));
            });
        }
    }
}
