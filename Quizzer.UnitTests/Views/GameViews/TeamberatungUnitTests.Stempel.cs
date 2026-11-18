using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.UnitTests.PlayThrough;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Stempel und Aufstellung nach „Teams umsetzen" (R4-3/R5-4): wer zwischen Ansehen und Wiederöffnen umgesetzt wird, steht an
    /// der Regel, an der Antwortsperre und in der Aufstellung des Hubs beim selben Team - ohne Buchung beim heutigen.
    /// </summary>
    public partial class TeamberatungUnitTests
    {
        /// <summary>Setzt einen Spieler um - im Speicher und in der Datenbank, wie „Teams umsetzen".</summary>
        private async Task SetzeUmAsync(int spieler, GameTeam team)
        {
            world!.Game.PlayerXGames.Single(x => x.PlayerId == Id(spieler)).GameTeamId = team.Id;

            await using var context = new Quizzer.Logic.Context.DataContext();
            context.PlayerXGames.Single(x => x.GameId == world.Game.Id && x.PlayerId == Id(spieler)).GameTeamId = team.Id;
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Anna (Sprecherin Rot) wird nach „Ansehen und schließen" zu Blau umgesetzt. Beim Wiederöffnen spricht für Rot Bert, für
        /// Blau Anna (nach Namen vor Clara) - Regel, Antwortsperre und Hub führen sie beim selben Team. Vorher folgte die Regel
        /// dem Stempel vom ersten Öffnen (Rot), der Hub der Zuordnung (Blau).
        /// </summary>
        [TestMethod]
        public async Task AfterOnlyViewingAMovedPlayerStandsWithTheSameTeamEverywhere()
        {
            var vm = await OeffneTeamberatungAsync();
            CollectionAssert.AreEquivalent(new[] { Id(0), Id(2) }, Hub.AnswerLock.Answerers!.ToArray(), "Vor dem Umsetzen sprechen nicht Anna und Clara.");
            await vm.ClosedForTestAsync();

            await SetzeUmAsync(0, blau);
            var erneut = new TestableCurrentQuestionViewModel { Coordinate = world!.Coordinate };
            await erneut.LoadForTestAsync();
            TestEnvironment.ThrowIfAnythingWasSwallowed();

            Assert.AreEqual(blau.Id, Karte(erneut, 0).Result.GameTeamId, "Die Karte trägt ohne jede Buchung den Stempel vom ersten Öffnen.");
            Assert.AreEqual(("Sprecher für Rot", "Sprecher für Blau"), (Karte(erneut, 1).SonderregelText, Karte(erneut, 0).SonderregelText),
                "Die Regel führt Anna nicht bei Blau.");
            CollectionAssert.AreEquivalent(new[] { Id(0), Id(1) }, Hub.AnswerLock.Answerers!.ToArray(), "Die Antwortsperre folgt dem alten Stempel.");
            Assert.AreEqual((blau.Id, Id(0)), (Hub.Teams.TeamOf(Id(0))?.TeamId, Hub.Teams.TeamOf(Id(0))?.SpeakerId), "Der Hub führt Anna anders als die Regel.");
            Assert.AreEqual((rot.Id, Id(1)), (Hub.Teams.TeamOf(Id(1))?.TeamId, Hub.Teams.TeamOf(Id(1))?.SpeakerId), "Der Hub nennt für Rot einen anderen Sprecher.");
        }
    }
}
