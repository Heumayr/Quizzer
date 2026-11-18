using Microsoft.EntityFrameworkCore;
using Quizzer.Logic.Context;

namespace Quizzer.Server.Tests
{
    public sealed partial class StatistikTeamTests
    {
        /// <summary>
        /// A01, Muster <c>SyncTests.TheLeaderboardShowsARecordOnlyOnceTheAccountPlaysInItsGame</c>: Teams und Teamsiege kommen
        /// nur aus Spielen, die die Statistik zeigt. Bob sitzt in Ginas offenem Abend (Blau gewinnt), nicht in ihrem geheimen.
        /// Zeigt sein Platz - am Abgleich vorbei, den <c>CheckTeamRefAsync</c> sonst abweist - auf das Team des geheimen Abends,
        /// steht dessen Name nirgends in seiner Antwort, und er hat kein Team. Gegenprobe: mit Platz im geheimen Abend schon.
        /// </summary>
        [TestMethod]
        public async Task TeamsOfAGameTheAccountDoesNotSeeStayHiddenEvenThroughAForeignSeat()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "GinaGeheim");
            var (bob, bobTokens) = await ServerTestHost.NewMemberAsync(factory, "BobGeheim");
            var bobId = bobTokens.Account.PlayerId;
            var geheimerName = "Geheim" + Guid.NewGuid().ToString("N")[..6];

            var geheim = Gespielt("Geheimer Abend", teamspiel: true);
            var geheimteam = Team(geheim, geheimerName, 7, 0);
            var gegner = Team(geheim, "Gegner", 2, 1);
            var gast = Gast("Geheimer Gast");
            await AnlegenAsync(gina, geheim, gast, geheimteam, gegner, Platz(geheim, gast.Id, geheimteam), Ergebnis(geheim, gast.Id, 300, geheimteam));

            var offen = Gespielt("Offener Abend", teamspiel: true);
            var rot = Team(offen, "Rot", 5, 0);
            var blau = Team(offen, "Blau", 4, 1);
            var zweiterGast = Gast("Roter Gast");
            var bobsPlatz = Platz(offen, bobId, blau);
            await AnlegenAsync(gina, offen, zweiterGast, rot, blau, bobsPlatz, Platz(offen, zweiterGast.Id, rot),
                Ergebnis(offen, bobId, 300, blau), Ergebnis(offen, zweiterGast.Id, 100, rot));

            var vorher = await StatistikAsync(bob);
            Assert.IsFalse(vorher.Games.Any(g => g.GameId == geheim.Game.Id), "Bob sieht den geheimen Abend.");
            Assert.AreEqual(("Blau", 1), (vorher.Games.Single(g => g.GameId == offen.Game.Id).TeamName, vorher.TeamWins),
                "Bobs Teamsieg im offenen Abend fehlt - die Probe misst sonst nichts.");

            await using (var context = new DataContext())
            {
                var platz = await context.PlayerXGames.SingleAsync(x => x.Id == bobsPlatz.Id);
                platz.GameTeamId = geheimteam.Id;
                await context.SaveChangesAsync();
            }

            var roh = await bob.GetStringAsync("/api/v1/me/statistics");
            Assert.IsFalse(roh.Contains(geheimerName, StringComparison.Ordinal), "Der Name eines Teams aus einem fremden Spiel steht in Bobs Statistik.");

            var nachher = await StatistikAsync(bob);
            var offeneZeile = nachher.Games.Single(g => g.GameId == offen.Game.Id);
            Assert.AreEqual((null, 0, 0), (offeneZeile.TeamName, offeneZeile.TeamRank, nachher.TeamWins),
                "Ein Platz, der auf ein fremdes Team zeigt, zählt als Team.");

            await PushAsync(gina, new() { Upserts = [SyncTestData.New(Platz(geheim, bobId, geheimteam))] });

            var mitPlatz = await StatistikAsync(bob);
            var geheimeZeile = mitPlatz.Games.Single(g => g.GameId == geheim.Game.Id);
            Assert.AreEqual((geheimerName, 1, 1), (geheimeZeile.TeamName, geheimeZeile.TeamRank, mitPlatz.TeamWins),
                "Mit Platz im Spiel fehlt das Team - die Kontrolle misst sonst nichts.");
        }
    }
}
