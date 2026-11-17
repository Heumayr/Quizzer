using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.Server.Tests
{
    /// <summary>Teams im Abgleich (M37 T1): Rang, Löschen und Umziehen in einem Push.</summary>
    public sealed partial class TeamTests
    {
        private static SyncDelete Loeschen(string typ, Guid id, string hash) => new() { Type = typ, Id = id, BaseHash = hash };

        /// <summary>Team und Zuteilung im selben Push: das Team ist am Server noch nicht da, die Zuteilung zählt es trotzdem.</summary>
        [TestMethod]
        public async Task ATeamAndItsAssignmentGoInOnePush()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var gruen = Team(abend.A.Game.Id, "Grün", 2);
            var hash = await HashAsync(abend.Gina, abend.MaxInA.Id);
            abend.MaxInA.GameTeamId = gruen.Id;

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.Changed(abend.MaxInA, hash), SyncTestData.New(gruen)] });

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            var stand = await SnapshotAsync(abend.Gina);
            Assert.AreEqual(gruen.Id, stand.Rows.Single(r => r.Id == abend.MaxInA.Id).Values[nameof(PlayerXGame.GameTeamId)].GetGuid());
        }

        /// <summary>
        /// Ein neues Team im selben Push zählt nur für sein eigenes Spiel: Gina leitet A und A2, das Team kommt neu in A2,
        /// Max sitzt in A - seine Zuteilung wird abgewiesen, das Team selbst geht durch.
        /// </summary>
        [TestMethod]
        public async Task ANewTeamOfAnotherGameInTheSamePushDoesNotCount()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var a2 = SyncTestData.NewGame("A2");
            Assert.AreEqual(0, (await PushAsync(abend.Gina, a2.AsNewPush())).Rejected.Count);
            var fremd = Team(a2.Game.Id, "A2-Team");
            var hash = await HashAsync(abend.Gina, abend.MaxInA.Id);
            abend.MaxInA.GameTeamId = fremd.Id;

            var result = await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(fremd), SyncTestData.Changed(abend.MaxInA, hash)] });

            AssertAccepted(result, fremd.Id, "Das Team in A2 wurde abgewiesen.");
            AssertRejected(result, abend.MaxInA.Id, SyncRejectReason.Forbidden, "Max in A wurde einem neuen Team aus A2 zugeteilt.");
        }

        /// <summary>
        /// Ergebnisse und ihr Team im selben Push löschen - so schickt es ein WPF, das „Alle Ergebnisse löschen" und danach
        /// das Team löscht. Die mitgelöschten Ergebnisse halten das Team nicht fest.
        /// </summary>
        [TestMethod]
        public async Task DeletingResultsAndTheirTeamInOnePushGoesThrough()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var ergebnis = new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = abend.A.Game.Id, GameGridCoordinateId = abend.A.Cell.Id, QuestionBaseId = abend.A.Question.Id,
                PlayerId = abend.MiaInA.PlayerId, Score = 100, GameTeamId = abend.Rot.Id,
            };
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(ergebnis)] })).Rejected.Count);
            var stand = (await SnapshotAsync(abend.Gina)).Rows.ToDictionary(r => r.Id, r => r.Hash);

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Deletes = [Loeschen(nameof(GameTeam), abend.Rot.Id, stand[abend.Rot.Id]), Loeschen(nameof(QuestionResult), ergebnis.Id, stand[ergebnis.Id])],
            });

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            Assert.IsFalse((await SnapshotAsync(abend.Gina)).Rows.Any(r => r.Id == abend.Rot.Id), "Das Team steht noch da.");
        }

        /// <summary>
        /// Ein Team löschen und seine Mitglieder im selben Push umsetzen - so schickt es ein WPF nach „Team löschen".
        /// <para>
        /// <b>Gemessen: hält das Freigeben nicht.</b> Der Server verfolgt den umgesetzten Platz schon beim Prüfen, und EF
        /// setzt bei NO ACTION den Verweis verfolgter Kinder selbst auf null - ohne <c>ReleaseTeamReferencesAsync</c> blieb
        /// dieser Test grün. Das Freigeben halten <see cref="DeletingATeamAloneReleasesItsMembers"/> und das Abholen in
        /// <c>ClientSyncTests.DeletingATeamAndMovingItsMembersTravelsInOnePush</c>.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task DeletingATeamAndMovingItsMembersGoInOnePush()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var blau = Team(abend.A.Game.Id, "Blau", 4);
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(blau)] })).Rejected.Count);

            var stand = (await SnapshotAsync(abend.Gina)).Rows.ToDictionary(r => r.Id, r => r.Hash);
            abend.MiaInA.GameTeamId = blau.Id;

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Deletes = [Loeschen(nameof(GameTeam), abend.Rot.Id, stand[abend.Rot.Id])],
                Upserts = [SyncTestData.Changed(abend.MiaInA, stand[abend.MiaInA.Id])],
            });

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            var nachher = await SnapshotAsync(abend.Gina);
            Assert.IsFalse(nachher.Rows.Any(r => r.Id == abend.Rot.Id), "Das Team Rot steht noch da.");
            Assert.AreEqual(blau.Id, nachher.Rows.Single(r => r.Id == abend.MiaInA.Id).Values[nameof(PlayerXGame.GameTeamId)].GetGuid(),
                "Mia ist nicht bei Blau angekommen.");
        }

        /// <summary>Das Team allein löschen: seine Mitglieder bleiben im Spiel, nur ohne Team - wie lokal im WPF.</summary>
        [TestMethod]
        public async Task DeletingATeamAloneReleasesItsMembers()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Deletes = [Loeschen(nameof(GameTeam), abend.Rot.Id, await HashAsync(abend.Gina, abend.Rot.Id))],
            });

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            var platz = (await SnapshotAsync(abend.Gina)).Rows.Single(r => r.Id == abend.MiaInA.Id);
            Assert.AreEqual(System.Text.Json.JsonValueKind.Null, platz.Values[nameof(PlayerXGame.GameTeamId)].ValueKind, "Mia hängt noch am gelöschten Team.");
        }

        /// <summary>
        /// Ein Team mit Ergebnissen lässt sich nur umbenennen - für den Gamemaster und den Admin. Ohne die Abweisung scheiterte
        /// der ganze Push am NO ACTION des Ergebnisses.
        /// </summary>
        [TestMethod]
        public async Task ATeamWithResultsCanOnlyBeRenamed()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var ergebnis = new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = abend.A.Game.Id, GameGridCoordinateId = abend.A.Cell.Id, QuestionBaseId = abend.A.Question.Id,
                PlayerId = abend.MiaInA.PlayerId, Score = 100, GameTeamId = abend.Rot.Id,
            };
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(ergebnis)] })).Rejected.Count);

            foreach (var wer in new[] { abend.Gina, abend.Admin })
            {
                var geloescht = await PushAsync(wer, new SyncPushRequest
                {
                    Deletes = [Loeschen(nameof(GameTeam), abend.Rot.Id, await HashAsync(abend.Gina, abend.Rot.Id))],
                });
                AssertRejected(geloescht, abend.Rot.Id, SyncRejectReason.Forbidden, "Ein Team mit Ergebnissen wurde gelöscht.");
            }

            var hash = await HashAsync(abend.Gina, abend.Rot.Id);
            abend.Rot.Name = "Feuer";
            AssertAccepted(await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.Changed(abend.Rot, hash)] }),
                abend.Rot.Id, "Umbenennen eines Teams mit Ergebnissen wurde abgewiesen.");
        }

        /// <summary>Ein Team wechselt nie das Spiel (V15) - auch nicht beim Admin, der beide Spiele schreiben darf.</summary>
        [TestMethod]
        public async Task ATeamStaysInItsGame()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var a2 = SyncTestData.NewGame("A2");
            Assert.AreEqual(0, (await PushAsync(abend.Gina, a2.AsNewPush())).Rejected.Count);

            foreach (var wer in new[] { abend.Gina, abend.Admin })
            {
                var hash = await HashAsync(abend.Gina, abend.Rot.Id);
                var umzug = abend.Rot.CloneWithoutReferences();
                umzug.GameId = a2.Game.Id;

                var result = await PushAsync(wer, new SyncPushRequest { Upserts = [SyncTestData.Changed(umzug, hash)] });

                AssertRejected(result, abend.Rot.Id, SyncRejectReason.Forbidden, "Das Team ist in ein anderes Spiel gewandert.");
            }
        }

        /// <summary>Das Spiel allein löschen: Teams samt Zuteilung und gestempeltem Ergebnis gehen mit (NO ACTION, der Server räumt).</summary>
        [TestMethod]
        public async Task DeletingTheGameTakesItsTeamsAlong()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var abend = await AbendAsync(factory);
            var ergebnis = new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = abend.A.Game.Id, GameGridCoordinateId = abend.A.Cell.Id, QuestionBaseId = abend.A.Question.Id,
                PlayerId = abend.MiaInA.PlayerId, Score = 100, GameTeamId = abend.Rot.Id,
            };
            Assert.AreEqual(0, (await PushAsync(abend.Gina, new SyncPushRequest { Upserts = [SyncTestData.New(ergebnis)] })).Rejected.Count);

            var result = await PushAsync(abend.Gina, new SyncPushRequest
            {
                Deletes = [Loeschen(nameof(Game), abend.A.Game.Id, await HashAsync(abend.Gina, abend.A.Game.Id))],
            });

            Assert.AreEqual(0, result.Rejected.Count, string.Join("; ", result.Rejected.Select(r => r.Message)));
            Assert.IsTrue(result.Deleted.Any(d => d.Id == abend.Rot.Id), "Der Server meldet das Team nicht als gelöscht.");
            Assert.IsFalse((await SnapshotAsync(abend.Gina)).Rows.Any(r => r.Id == abend.Rot.Id), "Das Team des gelöschten Spiels steht noch da.");
        }
    }
}
