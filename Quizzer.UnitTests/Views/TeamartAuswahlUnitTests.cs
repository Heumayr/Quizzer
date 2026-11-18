using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.HelperViewModels;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Teamarten in der Frageauswahl des Spielaufbaus (M37 P1, V23): ohne Teamspiel steht keine Art „nur im Teamspiel“ zur Wahl,
    /// wie bei den Sonderregeln. Eine, die schon auf der Zelle liegt, bleibt dort und wird benannt; der Spielstart meldet sie.
    /// </summary>
    [TestClass]
    public class TeamartAuswahlUnitTests
    {
        private static MinigameQuestion Minispiel(MinigameKind art) => new() { Id = Guid.NewGuid(), Designation = art.ToString(), Kind = art };

        private static bool NurImTeamspiel(MinigameKind art)
            => MinigameProfiles.TryFor(art, out var profil) && profil.TeamScoring == MinispielTeamwertung.NurImTeamspiel;

        private static List<MinigameKind> Arten(QuestionSelectorViewModel vm)
            => [.. vm.AvailableQuestions.OfType<MinigameQuestion>().Select(m => m.Kind)];

        [TestMethod]
        public void WithoutTeamsNoTeamOnlyKindIsOfferedWithTeamsEveryKindIs()
        {
            var alle = Enum.GetValues<MinigameKind>().Where(a => MinigameProfiles.TryFor(a, out _)).ToList();
            var teamarten = alle.Where(NurImTeamspiel).ToList();
            CollectionAssert.IsSubsetOf(new[] { MinigameKind.TugOfWar, MinigameKind.ProxyDuel, MinigameKind.Relay }, teamarten,
                "Die Teamarten aus dem Auftrag sind nicht „nur im Teamspiel“ - der Test prüfte dann nichts.");

            var gewoehnlich = new DefaultQuestion { Id = Guid.NewGuid(), Designation = "Hauptstadt" };
            var vm = new QuestionSelectorViewModel { AllQuestion = [gewoehnlich, .. alle.Select(Minispiel)] };
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid() };

            vm.SetDependencys(new Game(), zelle);

            CollectionAssert.AreEquivalent(alle.Except(teamarten).ToList(), Arten(vm), "Ohne Teamspiel stimmt das Angebot nicht.");
            CollectionAssert.Contains(vm.AvailableQuestions, gewoehnlich, "Die gewöhnliche Frage fehlt.");

            vm.SetDependencys(new Game { IsTeamGame = true }, zelle);

            CollectionAssert.AreEquivalent(alle, Arten(vm), "Im Teamspiel fehlt eine Art.");
        }

        /// <summary>Nichts still entfernen: die Teamart bleibt auf der Zelle, der Dialog benennt sie, der Spielstart meldet sie.</summary>
        [TestMethod]
        public void ATeamOnlyKindAlreadyOnTheCellStaysIsNamedAndTheStartStillReportsIt()
        {
            var tauziehen = Minispiel(MinigameKind.TugOfWar);
            var spiel = new Game { Id = Guid.NewGuid() };
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, QuestionBase = tauziehen, QuestionBaseId = tauziehen.Id };
            spiel.GameGridCoordinates.Add(zelle);
            var vm = new QuestionSelectorViewModel { AllQuestion = [tauziehen] };

            vm.SetDependencys(spiel, zelle);

            Assert.AreSame(tauziehen, vm.SelectedQuestion, "Die Teamart ist nicht mehr ausgewählt.");
            Assert.AreEqual(tauziehen.Id, zelle.QuestionBaseId, "Die Teamart wurde still von der Zelle genommen.");
            StringAssert.Contains(vm.TeamartHinweis, "nur im Teamspiel");
            Assert.AreEqual(MinigameChecks.OnlyInTeamGame, MinigameChecks.ForTeams(tauziehen, spiel).Single().Code, "Der Spielstart meldet die Teamart nicht mehr.");

            spiel.IsTeamGame = true;
            vm.SetDependencys(spiel, zelle);
            Assert.AreEqual(string.Empty, vm.TeamartHinweis, "Im Teamspiel steht ein Hinweis an der Teamart.");

            spiel.IsTeamGame = false;
            vm.SelectedQuestion = new DefaultQuestion { Id = Guid.NewGuid() };
            Assert.AreEqual(string.Empty, vm.TeamartHinweis, "An einer gewöhnlichen Frage steht ein Hinweis.");
        }
    }
}
