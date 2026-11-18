using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Die Aufstellung als Ganzes am Server (M37 P5, R1-7): höchstens acht Teams, keine Farbe und kein Name doppelt - wie
    /// <c>SaveLineupAsync</c> lokal. Gezählt wird der Stand nach dem Push, nicht die einzelne Zeile.
    /// </summary>
    public sealed partial class TeamTests
    {
        /// <summary>Rot (Farbe 5) steht schon; ein neues Team gleicher Farbe, gleichen Namens oder als neuntes geht nicht durch.</summary>
        [TestMethod]
        [DataRow("Grün", 5, 0, DisplayName = "Farbe doppelt")]
        [DataRow(" rot ", 2, 0, DisplayName = "Name doppelt")]
        [DataRow("Neun", 0, 7, DisplayName = "neuntes Team")]
        public async Task ALineupBreakingTheLimitsIsInvalid(string name, int farbe, int weitere)
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var auffuellen = Enumerable.Range(0, weitere).Select(i => Team(abend.A.Game.Id, "Team " + i, i < 5 ? i : i + 1)).ToList();
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = auffuellen.Select(SyncTestData.New).ToList() })).Rejected.Count,
                "Das Auffüllen auf acht Teams wurde schon abgewiesen.");
            var neu = Team(abend.A.Game.Id, name, farbe);

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(neu)] });

            AssertRejected(result, neu.Id, SyncRejectReason.Invalid, "Die Aufstellung verletzt eine Grenze und ging durch.");
            Assert.AreEqual(1 + weitere, (await SnapshotAsync(abend.Gina)).Rows.Count(r => r.Type == nameof(GameTeam) && r.Values[nameof(GameTeam.GameId)].GetGuid() == abend.A.Game.Id));
        }

        /// <summary>
        /// Gegenrichtung: zwei Teams tauschen ihre Farben, und ein gelöschtes Team gibt seine Farbe frei - beides in einem Push.
        /// Eine Prüfung je Zeile wiese hier ab.
        /// </summary>
        [TestMethod]
        public async Task SwappingColoursAndReusingAFreedOneGoThrough()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var blau = Team(abend.A.Game.Id, "Blau", 4);
            var gelb = Team(abend.A.Game.Id, "Gelb", 1);
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(blau), SyncTestData.New(gelb)] })).Rejected.Count);
            var stand = (await SnapshotAsync(abend.Gina)).Rows.ToDictionary(r => r.Id, r => r.Hash);
            (abend.Rot.ColorIndex, blau.ColorIndex) = (4, 5);
            var neu = Team(abend.A.Game.Id, "Gelb", 1);

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Upserts = [SyncTestData.Changed(abend.Rot, stand[abend.Rot.Id]), SyncTestData.Changed(blau, stand[blau.Id]), SyncTestData.New(neu)],
                Deletes = [Loeschen(nameof(GameTeam), gelb.Id, stand[gelb.Id])],
            });

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            Assert.IsNull(TeamGrenzen.AufstellungProblem([abend.Rot, blau, neu]), "Die Probe selbst verletzt eine Grenze.");
        }

        /// <summary>Ein Team, das gerade ein anderes Spiel trägt, zählt dort nicht mit: Bobs Blau (Farbe 4) stört Ginas Blau nicht.</summary>
        [TestMethod]
        public async Task TeamsOfAnotherGameDoNotCount()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(Team(abend.A.Game.Id, abend.BobsBlau.Name, abend.BobsBlau.ColorIndex))] });

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
        }
    }
}
