using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Sync;

namespace Quizzer.Server.Tests
{
    /// <summary>Ein Team mit Ergebnissen löschen (M37 P5): gezählt wird, was nach dem Push am Team hängt (R1-1).</summary>
    public sealed partial class TeamTests
    {
        private static QuestionResult ErgebnisVonMia(Abend abend) => new()
        {
            Id = Guid.NewGuid(), GameId = abend.A.Game.Id, GameGridCoordinateId = abend.A.Cell.Id, QuestionBaseId = abend.A.Question.Id,
            PlayerId = abend.MiaInA.PlayerId, Score = 100, GameTeamId = abend.Rot.Id,
        };

        private static Guid? StempelAmServer(SyncSnapshot stand, Guid ergebnis)
        {
            var wert = stand.Rows.Single(r => r.Id == ergebnis).Values[nameof(QuestionResult.GameTeamId)];
            return wert.ValueKind == System.Text.Json.JsonValueKind.Null ? null : wert.GetGuid();
        }

        /// <summary>
        /// R1-1: das Ergebnis mit veraltetem Hash gelöscht, dazu das Team. Das Ergebnis wird abgewiesen - also hängt es noch am
        /// Team, und das Team bleibt stehen. Vorher zählte die rohe Anfrage: das Team ging, das Ergebnis verlor still den Stempel.
        /// </summary>
        [TestMethod]
        public async Task ARejectedResultDeletionKeepsItsTeam()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var ergebnis = ErgebnisVonMia(abend);
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(ergebnis)] })).Rejected.Count);
            var veraltet = await HashAsync(abend.Gina, ergebnis.Id);
            ergebnis.Score = 300;
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.Changed(ergebnis, veraltet)] })).Rejected.Count);

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Deletes = [Loeschen(nameof(QuestionResult), ergebnis.Id, veraltet), Loeschen(nameof(GameTeam), abend.Rot.Id, await HashAsync(abend.Gina, abend.Rot.Id))],
            });

            AssertRejected(result, ergebnis.Id, SyncRejectReason.Conflict, "Das Ergebnis mit altem Hash wurde gelöscht.");
            AssertRejected(result, abend.Rot.Id, SyncRejectReason.Forbidden, "Das Team ging, obwohl sein Ergebnis bleibt.");
            var stand = await SnapshotAsync(abend.Gina);
            Assert.IsTrue(stand.Rows.Any(r => r.Id == abend.Rot.Id), "Das Team steht nicht mehr da.");
            Assert.AreEqual(abend.Rot.Id, StempelAmServer(stand, ergebnis.Id), "Das Ergebnis hat seinen Stempel still verloren.");
        }

        /// <summary>
        /// R1-1, Wettlauf: zwischen Prüfung und Einspielen kommt ein gestempeltes Ergebnis dazu - nachgestellt, indem das
        /// Einspielen ohne die Prüfung läuft. Der Server gibt es nicht frei; das Löschen scheitert, und nichts ändert sich.
        /// </summary>
        [TestMethod]
        public async Task TheServerNeverReleasesAResultOfADeletedTeam()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var ergebnis = ErgebnisVonMia(abend);
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(ergebnis)] })).Rejected.Count);

            await using (var store = new SyncStore(ServerTestHost.ConnectionString))
            {
                await Assert.ThrowsAsync<DbUpdateException>(
                    () => store.ApplyAsync([], [new SyncKey(nameof(GameTeam), abend.Rot.Id)], strict: true));
            }

            var stand = await SnapshotAsync(abend.Gina);
            Assert.IsTrue(stand.Rows.Any(r => r.Id == abend.Rot.Id), "Das Team ist trotz seines Ergebnisses gelöscht.");
            Assert.AreEqual(abend.Rot.Id, StempelAmServer(stand, ergebnis.Id), "Der Server hat das Ergebnis still freigegeben.");
        }

        /// <summary>
        /// Umgestempelt im selben Push: das Ergebnis zeigt danach auf Blau, Rot hält nichts mehr - so schickt es ein WPF, das
        /// einen Umgesetzten beim Öffnen neu stempelt (R4-3) und danach Rot löscht. Die Gegenrichtung zur Abweisung oben.
        /// Mit einer Frage im Push leert der Server seine verfolgten Einträge (Fragetyp umwandeln) - dann trägt nur das
        /// ausdrückliche Freigeben, nicht EF.
        /// </summary>
        [TestMethod]
        [DataRow(false, DisplayName = "nur Teamzeilen")]
        [DataRow(true, DisplayName = "mit Frage im Push")]
        public async Task AResultRestampedInTheSamePushReleasesItsTeam(bool mitFrage)
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var blau = Team(abend.A.Game.Id, "Blau", 4);
            var ergebnis = ErgebnisVonMia(abend);
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(blau), SyncTestData.New(ergebnis)] })).Rejected.Count);
            var stand = (await SnapshotAsync(abend.Gina)).Rows.ToDictionary(r => r.Id, r => r.Hash);
            ergebnis.GameTeamId = blau.Id;
            abend.MiaInA.GameTeamId = blau.Id;

            var push = new SyncPushRequest
            {
                Deletes = [Loeschen(nameof(GameTeam), abend.Rot.Id, stand[abend.Rot.Id])],
                Upserts = [SyncTestData.Changed(ergebnis, stand[ergebnis.Id]), SyncTestData.Changed(abend.MiaInA, stand[abend.MiaInA.Id])],
            };

            if (mitFrage)
            {
                abend.A.Question.Notes = "nachgeschärft";
                push.Upserts.Add(SyncTestData.Changed(abend.A.Question, stand[abend.A.Question.Id]));
            }

            var result = await PushAsync(abend.Gina, push);

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            var nachher = await SnapshotAsync(abend.Gina);
            Assert.IsFalse(nachher.Rows.Any(r => r.Id == abend.Rot.Id), "Rot steht noch da.");
            Assert.AreEqual(blau.Id, StempelAmServer(nachher, ergebnis.Id), "Das Ergebnis ist nicht bei Blau.");
        }

        /// <summary>Eine Ergebniszeile ohne das Stempelfeld stempelt nicht um - der Stempel des Servers bleibt und hält das Team.</summary>
        [TestMethod]
        public async Task AResultRowWithoutTheStampFieldKeepsItsTeam()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var ergebnis = ErgebnisVonMia(abend);
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(ergebnis)] })).Rejected.Count);
            var stand = (await SnapshotAsync(abend.Gina)).Rows.ToDictionary(r => r.Id, r => r.Hash);
            ergebnis.Score = 300;
            var ohneStempel = SyncTestData.Changed(ergebnis, stand[ergebnis.Id]);
            Assert.IsTrue(ohneStempel.Row.Values.Remove(nameof(QuestionResult.GameTeamId)), "Die Zeile trug den Stempel gar nicht.");

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Upserts = [ohneStempel],
                Deletes = [Loeschen(nameof(GameTeam), abend.Rot.Id, stand[abend.Rot.Id])],
            });

            AssertRejected(result, abend.Rot.Id, SyncRejectReason.Forbidden, "Rot ging, obwohl das Ergebnis seinen Stempel behält.");
            Assert.AreEqual(abend.Rot.Id, StempelAmServer(await SnapshotAsync(abend.Gina), ergebnis.Id), "Das Ergebnis hat seinen Stempel verloren.");
        }
    }
}
