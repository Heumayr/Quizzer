using Microsoft.EntityFrameworkCore;
using Quizzer.Client;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// M9 Ende zu Ende: „ich am pc vorbereite und dann am laptop spiele" - mit echtem Server,
    /// zwei lokalen Datenbanken und demselben Abgleich wie im Programm.
    /// </summary>
    [TestClass]
    public sealed class ClientSyncTests
    {
        private static async Task<(ClientHarness Pc, ClientHarness Laptop, Guid Me)> SignedInPairAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
        {
            var (_, tokens) = await ServerTestHost.NewMemberAsync(factory, "Franz");
            var pc = await ClientHarness.CreateAsync(factory, "Pc");
            var laptop = await ClientHarness.CreateAsync(factory, "Laptop");

            await pc.Api.LoginAsync(tokens.Account.UserName, "Mitglieds-Kennwort-123");
            await laptop.Api.LoginAsync(tokens.Account.UserName, "Mitglieds-Kennwort-123");

            // Wie im Programm: nach der Anmeldung wird zuerst abgeholt - erst dann gibt es den
            // eigenen Mitspieler lokal, an dem jedes Spiel haengt.
            await pc.Sync.PullAsync();
            await laptop.Sync.PullAsync();

            return (pc, laptop, tokens.Account.PlayerId);
        }

        private static async Task<SyncTestData.GameSet> PrepareGameLocallyAsync(ClientHarness client, Guid me)
        {
            var set = SyncTestData.NewGame("Abend");
            set.Game.ModeratorPlayerId = me;
            set.Question.OwnerPlayerId = me;

            await using var local = client.Local();
            local.AddRange(set.Category, set.Question, set.Game, set.Cell);
            local.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = set.Game.Id, PlayerId = me });
            await local.SaveChangesAsync();

            return set;
        }

        [TestMethod]
        public async Task PreparedOnThePcPlayedOnTheLaptopAndTheScoreComesBack()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);

            var pushed = await pc.Sync.PushAsync();
            Assert.AreEqual(0, pushed.Rejected.Count, string.Join("; ", pushed.Rejected.Select(r => r.Message)));

            // Laptop holt alles und spielt eine Zelle.
            await laptop.Sync.PullAsync();
            await using (var local = laptop.Local())
            {
                var cell = await local.GameGridCoordinates.SingleAsync(c => c.Id == set.Cell.Id);
                cell.IsDone = true;
                local.QuestionResults.Add(new QuestionResult
                {
                    Id = Guid.NewGuid(), GameId = set.Game.Id, GameGridCoordinateId = cell.Id,
                    QuestionBaseId = set.Question.Id, PlayerId = me, Score = 300, RightCount = 1,
                });
                await local.SaveChangesAsync();
            }

            Assert.AreEqual(2, await laptop.Sync.CountPendingAsync(), "Zelle und Ergebnis sind offen.");
            var played = await laptop.Sync.PushAsync();
            Assert.AreEqual(2, played.Accepted);
            Assert.AreEqual(0, await laptop.Sync.CountPendingAsync(), "Nach dem Push ist nichts mehr offen.");

            // Zurueck am PC: der Spielstand ist da.
            await pc.Sync.PullAsync();
            await using (var local = pc.Local())
            {
                Assert.IsTrue((await local.GameGridCoordinates.SingleAsync(c => c.Id == set.Cell.Id)).IsDone);
                Assert.AreEqual(300, (await local.QuestionResults.SingleAsync(r => r.GameId == set.Game.Id)).Score);
            }
        }

        [TestMethod]
        public async Task MediaTravelFromThePcToTheLaptop()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);
            var fileName = $"bild_{Guid.NewGuid():N}.png";
            Directory.CreateDirectory(Path.Combine(pc.MediaRoot, "Resources"));
            await File.WriteAllTextAsync(Path.Combine(pc.MediaRoot, "Resources", fileName), "bildinhalt");

            await using (var local = pc.Local())
            {
                local.QuestionStepResources.Add(new QuestionStepResource
                {
                    Id = Guid.NewGuid(), QuestionBaseId = set.Question.Id, ResourceFileName = fileName, Designation = "Bild",
                });
                await local.SaveChangesAsync();
            }

            var pushed = await pc.Sync.PushAsync();
            Assert.AreEqual(1, pushed.MediaUploaded);

            var pulled = await laptop.Sync.PullAsync();
            Assert.AreEqual(0, pulled.MediaProblems.Count, string.Join("; ", pulled.MediaProblems));
            Assert.AreEqual("bildinhalt", await File.ReadAllTextAsync(Path.Combine(laptop.MediaRoot, "Resources", fileName)));
        }

        [TestMethod]
        public async Task AConflictIsReportedAndThePullBringsTheServerState()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);
            await pc.Sync.PushAsync();
            await laptop.Sync.PullAsync();

            await RenameGameAsync(pc, set.Game.Id, "Vom PC");
            await RenameGameAsync(laptop, set.Game.Id, "Vom Laptop");

            Assert.AreEqual(0, (await pc.Sync.PushAsync()).Rejected.Count);

            var second = await laptop.Sync.PushAsync();
            Assert.AreEqual(1, second.Rejected.Count);
            Assert.AreEqual(SyncRejectReason.Conflict, second.Rejected[0].Reason);

            // Abgewiesenes geht nicht bei jedem Speichern erneut hinaus ...
            Assert.AreEqual(0, await laptop.Sync.CountPendingAsync());
            Assert.IsFalse((await laptop.Sync.PushAsync()).HadChanges);

            // ... und das naechste Abholen bringt den Stand des Servers.
            await laptop.Sync.PullAsync();
            await using var local = laptop.Local();
            Assert.AreEqual("Vom PC", (await local.Games.SingleAsync(g => g.Id == set.Game.Id)).Designation);
        }

        /// <summary>
        /// V4: eine Ablehnung bleibt mit Grund sichtbar, bis sie aufgeloest ist - vorher meldete der
        /// naechste Push „Abgeglichen", und die Aenderung lag still liegen.
        /// </summary>
        [TestMethod]
        public async Task ARejectionStaysVisibleWithItsReasonUntilItIsResolved()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);
            await pc.Sync.PushAsync();
            await laptop.Sync.PullAsync();

            await RenameGameAsync(pc, set.Game.Id, "Vom PC");
            await RenameGameAsync(laptop, set.Game.Id, "Vom Laptop");
            await pc.Sync.PushAsync();
            await laptop.Sync.PushAsync();

            var offen = await laptop.Sync.GetRejectionsAsync();
            Assert.AreEqual(1, offen.Count);
            Assert.AreEqual(SyncRejectReason.Conflict, offen[0].Reason);
            Assert.AreEqual("Vom Laptop", offen[0].Bezeichnung);
            Assert.IsFalse(offen[0].Loeschung);
            Assert.IsFalse(offen[0].ErstAnlage, "Der Laptop hatte das Spiel vom Server - kein Erstanlegen (V12).");
            Assert.IsFalse(string.IsNullOrWhiteSpace(offen[0].Message));

            // Ein spaeterer Durchlauf ohne neue Ablehnung meldet sie weiter.
            using (var background = new BackgroundSync(laptop.Sync))
            {
                await background.FlushAsync();
                Assert.AreEqual(SyncStatus.Rejected, background.Status.Status, background.Status.Message);
                Assert.AreEqual(1, background.Status.Offen);
            }

            // Erneut senden: der Konflikt besteht noch, also wieder abgewiesen - und wieder sichtbar.
            var erneut = await laptop.Sync.ResendRejectedAsync();
            Assert.AreEqual(1, erneut.Rejected.Count);
            Assert.AreEqual(1, laptop.Sync.CountRejections());

            // Serverstand uebernehmen raeumt auf.
            await laptop.Sync.PullAsync();
            Assert.AreEqual(0, laptop.Sync.CountRejections());
            Assert.AreEqual(0, (await laptop.Sync.GetRejectionsAsync()).Count);
        }

        /// <summary>
        /// 2026-10-22: offline gespielt, dann ist der Server wieder da. Mit dem gespeicherten
        /// Erneuerungsschlüssel geht es ohne Kennwort weiter, und der Abgleich schickt, was
        /// offline gespeichert wurde - aus dem Zustand von vor dem Ausfall.
        /// </summary>
        [TestMethod]
        public async Task WhatWasSavedOfflineGoesOutOnceTheServerIsBack()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);
            await pc.Sync.PushAsync();

            // „Offline": nur lokal gespeichert, nichts geht hinaus.
            await RenameGameAsync(pc, set.Game.Id, "Offline gespielt");

            var (fortgesetzt, sync) = await pc.ReconnectAsync(factory);
            Assert.IsTrue(fortgesetzt, "Ohne Kennwort ging es nicht weiter.");
            Assert.AreEqual(1, await sync.CountPendingAsync(), "Die offline gespeicherte Aenderung ist nicht mehr offen.");

            var push = await sync.PushAsync();
            Assert.AreEqual(0, push.Rejected.Count, string.Join("; ", push.Rejected.Select(r => r.Message)));

            await laptop.Sync.PullAsync();
            await using var local = laptop.Local();
            Assert.AreEqual("Offline gespielt", (await local.Games.SingleAsync(g => g.Id == set.Game.Id)).Designation);
        }

        /// <summary>Ist der Erneuerungsschlüssel weg, gibt es keinen stillen Neuanfang - dann braucht es das Kennwort.</summary>
        [TestMethod]
        public async Task WithoutAValidRefreshTokenReconnectingNeedsTheUser()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, _) = await SignedInPairAsync(factory);
            await using var __ = pc;
            await using var ___ = laptop;

            pc.Tokens.Clear();

            var (fortgesetzt, _) = await pc.ReconnectAsync(factory);
            Assert.IsFalse(fortgesetzt);
        }

        /// <summary>Erneut senden bringt durch, was inzwischen geht.</summary>
        [TestMethod]
        public async Task ResendingGetsThroughOnceTheCauseIsGone()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);
            await pc.Sync.PushAsync();
            await laptop.Sync.PullAsync();

            await RenameGameAsync(pc, set.Game.Id, "Vom PC");
            await RenameGameAsync(laptop, set.Game.Id, "Vom Laptop");
            await pc.Sync.PushAsync();
            await laptop.Sync.PushAsync();
            Assert.AreEqual(1, laptop.Sync.CountRejections());

            // Der PC nimmt seine Aenderung zurueck auf genau den Stand, den der Laptop zuletzt
            // kannte - dann passt dessen Hash wieder, und der Laptop kommt durch.
            await RenameGameAsync(pc, set.Game.Id, set.Game.Designation);
            await pc.Sync.PushAsync();

            var erneut = await laptop.Sync.ResendRejectedAsync();
            Assert.AreEqual(0, erneut.Rejected.Count, string.Join("; ", erneut.Rejected.Select(r => r.Message)));
            Assert.AreEqual(0, laptop.Sync.CountRejections());
        }

        [TestMethod]
        public async Task ADeletedCellDisappearsOnTheOtherMachine()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);
            await pc.Sync.PushAsync();
            await laptop.Sync.PullAsync();

            await using (var local = pc.Local())
            {
                local.GameGridCoordinates.Remove(await local.GameGridCoordinates.SingleAsync(c => c.Id == set.Cell.Id));
                await local.SaveChangesAsync();
            }

            var pushed = await pc.Sync.PushAsync();
            Assert.AreEqual(1, pushed.Deleted);

            await laptop.Sync.PullAsync();
            await using var laptopDb = laptop.Local();
            Assert.IsFalse(await laptopDb.GameGridCoordinates.AnyAsync(c => c.Id == set.Cell.Id));
        }

        /// <summary>M35 C9: Rohwert, sein Zeitpunkt und die Klaus am Ergebnis gehen vom PC über den Server zum Laptop.</summary>
        [TestMethod]
        public async Task ARecordAndItsStealsTravelToTheOtherMachine()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);
            var reaktion = new MinigameQuestion { Id = Guid.NewGuid(), Designation = "Reaktion", CategoryId = set.Category.Id, OwnerPlayerId = me, Points = 100 };
            Quizzer.DataModels.Questions.Minispiele.MinigameProfiles.For(MinigameKind.Reaction).ApplyDefaults(reaktion);
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = set.Game.Id, QuestionBaseId = reaktion.Id, X = 1, Y = 0 };
            var gemessen = new DateTimeOffset(2026, 11, 16, 21, 30, 0, TimeSpan.FromHours(1));
            var ergebnis = new QuestionResult
            {
                Id = Guid.NewGuid(), GameId = set.Game.Id, GameGridCoordinateId = zelle.Id, QuestionBaseId = reaktion.Id,
                PlayerId = me, Score = 100, RightCount = 1, RecordValue = 187, RecordedAt = gemessen, Steals = 2,
            };
            await using (var local = pc.Local())
            {
                local.AddRange(reaktion, zelle, ergebnis);
                await local.SaveChangesAsync();
            }

            var pushed = await pc.Sync.PushAsync();
            Assert.AreEqual(0, pushed.Rejected.Count, string.Join("; ", pushed.Rejected.Select(r => r.Message)));

            await laptop.Sync.PullAsync();
            await using var laptopDb = laptop.Local();
            var angekommen = await laptopDb.QuestionResults.SingleAsync(r => r.Id == ergebnis.Id);
            Assert.AreEqual((187, gemessen, 2), (angekommen.RecordValue, angekommen.RecordedAt, angekommen.Steals),
                "Rohwert, Zeitpunkt oder Klaus sind am Laptop nicht angekommen.");
        }

        /// <summary>M35 C7: ein Joker geht vom PC zum Laptop, das Zurücknehmen am Laptop wieder zum PC.</summary>
        [TestMethod]
        public async Task AJokerTravelsToTheOtherMachineAndSoDoesItsWithdrawal()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);
            var joker = new PlayerJoker
            {
                Id = Guid.NewGuid(), GameId = set.Game.Id, PlayerId = me, Kind = JokerKind.Shield,
                Reason = "Kopfrechnen gewonnen", GrantedAt = DateTimeOffset.UtcNow,
            };
            await using (var local = pc.Local())
            {
                local.PlayerJokers.Add(joker);
                await local.SaveChangesAsync();
            }

            var pushed = await pc.Sync.PushAsync();
            Assert.AreEqual(0, pushed.Rejected.Count, string.Join("; ", pushed.Rejected.Select(r => r.Message)));

            await laptop.Sync.PullAsync();
            await using (var local = laptop.Local())
            {
                var angekommen = await local.PlayerJokers.SingleAsync(j => j.Id == joker.Id);
                Assert.AreEqual((JokerKind.Shield, "Kopfrechnen gewonnen", me), (angekommen.Kind, angekommen.Reason, angekommen.PlayerId));
                Assert.AreEqual(joker.GrantedAt, angekommen.GrantedAt);

                local.PlayerJokers.Remove(angekommen);
                await local.SaveChangesAsync();
            }

            var zurueck = await laptop.Sync.PushAsync();
            Assert.AreEqual(1, zurueck.Deleted);

            await pc.Sync.PullAsync();
            await using var pcDb = pc.Local();
            Assert.IsFalse(await pcDb.PlayerJokers.AnyAsync(j => j.Id == joker.Id), "Das Zurücknehmen ist am PC nicht angekommen.");
        }

        [TestMethod]
        public async Task AQuestionThatChangedItsTypeArrivesAsTheNewType()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, laptop, me) = await SignedInPairAsync(factory);
            await using var _ = pc;
            await using var __ = laptop;

            var set = await PrepareGameLocallyAsync(pc, me);
            await pc.Sync.PushAsync();
            await laptop.Sync.PullAsync();

            using (var controller = new QuestionBasesController())
            {
                controller.Context.Dispose();
                controller.Context = pc.Local();
                await controller.ConvertTypeAsync(set.Question.Id, QuestionType.MultipleChoice);
            }

            var pushed = await pc.Sync.PushAsync();
            Assert.AreEqual(0, pushed.Rejected.Count, string.Join("; ", pushed.Rejected.Select(r => r.Message)));

            await laptop.Sync.PullAsync();
            await using var local = laptop.Local();
            var question = await local.QuestionBases.SingleAsync(q => q.Id == set.Question.Id);
            Assert.IsInstanceOfType<MultipleChoiceQuestion>(question);
            Assert.IsTrue(await local.GameGridCoordinates.AnyAsync(c => c.QuestionBaseId == set.Question.Id), "Die Zelle zeigt weiter auf die Frage.");
        }

        [TestMethod]
        public async Task ABackgroundPushFollowsALocalSave()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var (pc, _, me) = await SignedInPairAsync(factory);
            await using var __ = pc;

            using var background = new BackgroundSync(pc.Sync, TimeSpan.FromMilliseconds(200));
            var upToDate = new TaskCompletionSource();
            background.StatusChanged += (_, s) => { if (s.Status == SyncStatus.UpToDate && s.LastSuccess != null) upToDate.TrySetResult(); };
            background.Start();

            var set = await PrepareGameLocallyAsync(pc, me);

            await upToDate.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await WaitUntilAsync(async () => await pc.Sync.CountPendingAsync() == 0);

            var snapshot = await pc.Api.GetSnapshotAsync();
            Assert.IsTrue(snapshot.Rows.Any(r => r.Id == set.Game.Id), "Das Spiel ist ohne ausdruecklichen Push beim Server angekommen.");
        }

        private static async Task WaitUntilAsync(Func<Task<bool>> condition)
        {
            for (var i = 0; i < 100; i++)
            {
                if (await condition())
                    return;
                await Task.Delay(100);
            }

            Assert.Fail("Die Bedingung trat nicht ein.");
        }

        private static async Task RenameGameAsync(ClientHarness client, Guid gameId, string name)
        {
            await using var local = client.Local();
            (await local.Games.SingleAsync(g => g.Id == gameId)).Designation = name;
            await local.SaveChangesAsync();
        }
    }
}
