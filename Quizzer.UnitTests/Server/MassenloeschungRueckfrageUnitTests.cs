using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Server;

namespace Quizzer.UnitTests.Server
{
    /// <summary>
    /// V06 (M37 D3): hält die Massenlösch-Sperre einen ausdrücklichen Abgleich an („Jetzt abgleichen", „Vom Server neu
    /// laden", Anmeldung), fragt das Programm mit Zahlen nach. Nur bei Ja läuft es noch einmal - mit genau der genannten
    /// Freigabe. Den Abgleich selbst prüfen die Servertests (<c>ClientSafetyTests</c>); eine echte Serversitzung lässt
    /// sich hier nicht herstellen.
    /// </summary>
    [TestClass]
    public class MassenloeschungRueckfrageUnitTests
    {
        private static readonly MassDeletion Gesperrt = new(12, 2, 20,
            [.. Enumerable.Range(0, 14).Select(i => new SyncKey(i < 2 ? "Game" : "DefaultQuestion", Guid.NewGuid()))]);

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        /// <summary>Ein Lauf, der ohne Freigabe in die Sperre läuft und mit Freigabe durchgeht - er schreibt mit, womit.</summary>
        private static Func<MassDeletion?, Task<int>> Lauf(List<MassDeletion?> aufrufe)
            => freigabe =>
            {
                aufrufe.Add(freigabe);
                return freigabe == null ? throw new SyncSafetyException("gesperrt", Gesperrt) : Task.FromResult(1);
            };

        [TestMethod]
        public async Task NoKeepsTheGuardAndAsksWithTheNumbers()
        {
            var prompt = new RecordingUserPrompt(answer: false);
            UserPrompt.Current = prompt;
            var aufrufe = new List<MassDeletion?>();

            await Assert.ThrowsExactlyAsync<SyncSafetyException>(() => MassDeletionPrompt.RunAskingAsync(Lauf(aufrufe), "Vom Server laden"));

            Assert.AreEqual(1, aufrufe.Count, "Trotz „Nein“ lief der Abgleich noch einmal.");
            Assert.AreEqual(1, prompt.Confirms.Count, "Es wurde nicht gefragt.");
            StringAssert.Contains(prompt.Confirms[0].Message, "12 Fragen und 2 Spiele");
        }

        [TestMethod]
        public async Task YesRunsOnceMoreWithExactlyTheNamedDeletion()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            var aufrufe = new List<MassDeletion?>();

            Assert.AreEqual(1, await MassDeletionPrompt.RunAskingAsync(Lauf(aufrufe), "Anmeldung"));

            Assert.AreEqual(2, aufrufe.Count);
            Assert.IsNull(aufrufe[0], "Der erste Lauf hatte schon eine Freigabe.");
            Assert.AreSame(Gesperrt, aufrufe[1], "Der zweite Lauf hatte nicht die genannte Freigabe.");
        }

        /// <summary>Ohne Sperre wird nicht gefragt - ein Dialog bei jedem Abgleich würde ungelesen weggeklickt.</summary>
        [TestMethod]
        public async Task WithoutTheGuardNothingIsAsked()
        {
            var prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;

            Assert.AreEqual(7, await MassDeletionPrompt.RunAskingAsync(_ => Task.FromResult(7), "Vom Server laden"));
            Assert.IsFalse(await MassDeletionPrompt.ApprovePendingAsync(null, _ => Task.CompletedTask, "Jetzt abgleichen"));
            Assert.AreEqual(0, prompt.Confirms.Count);
        }

        /// <summary>„Jetzt abgleichen": was der Hintergrund gemeldet hat, geht nur bei Ja hinaus, und zwar genau das.</summary>
        [TestMethod]
        public async Task SyncNowSendsTheReportedDeletionOnlyOnYes()
        {
            var gesendet = new List<MassDeletion>();

            UserPrompt.Current = new RecordingUserPrompt(answer: false);
            Assert.IsFalse(await MassDeletionPrompt.ApprovePendingAsync(Gesperrt, f => { gesendet.Add(f); return Task.CompletedTask; }, "Jetzt abgleichen"));
            Assert.AreEqual(0, gesendet.Count, "Trotz „Nein“ gesendet.");

            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            Assert.IsTrue(await MassDeletionPrompt.ApprovePendingAsync(Gesperrt, f => { gesendet.Add(f); return Task.CompletedTask; }, "Jetzt abgleichen"));
            Assert.AreEqual(1, gesendet.Count);
            Assert.AreSame(Gesperrt, gesendet[0]);
        }
    }
}
