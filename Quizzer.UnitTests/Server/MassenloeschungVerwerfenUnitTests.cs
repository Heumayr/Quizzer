using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Server;

namespace Quizzer.UnitTests.Server
{
    /// <summary>
    /// V17 (M37 D5, umkehrbare Annahme F-M37-15): nach „Nein" bei der Massenlösch-Sperre bietet das Programm an, die
    /// Löschung zu verwerfen und vom Server zu laden - mit Zahlen, an allen drei Stellen, die die Sperre melden. Was das
    /// Verwerfen am Abgleich tut, prüfen die Servertests (<c>ClientSafetyTests.Verwerfen</c>).
    /// </summary>
    [TestClass]
    public class MassenloeschungVerwerfenUnitTests
    {
        private static readonly MassDeletion Gesperrt = new(12, 2, 20,
            [.. Enumerable.Range(0, 14).Select(i => new SyncKey(i < 2 ? "Game" : "DefaultQuestion", Guid.NewGuid()))]);

        private static readonly MassDeletionDiscard Vorbereitet = new(Gesperrt, Gesperrt, 17, 3);

        /// <summary>Antwortet der Reihe nach und schreibt die Texte mit.</summary>
        private sealed class AntwortFolge(params bool[] antworten) : IUserPrompt
        {
            private readonly Queue<bool> offen = new(antworten);

            public List<string> Fragen { get; } = [];

            public bool Confirm(string message, string caption)
            {
                Fragen.Add(message);
                return offen.Dequeue();
            }

            public void Inform(string message, string caption = "")
            {
            }
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        private static Func<MassDeletion?, Task<string>> Lauf(List<MassDeletion?> aufrufe)
            => freigabe =>
            {
                aufrufe.Add(freigabe);
                return freigabe == null ? throw new SyncSafetyException("gesperrt", Gesperrt) : Task.FromResult("freigegeben");
            };

        private static Func<MassDeletion, Task<string?>> Ausweg(List<MassDeletionDiscard> angewandt, List<MassDeletion> vorbereitet)
            => gesperrt => MassDeletionPrompt.OfferDiscardAsync(
                () => { vorbereitet.Add(gesperrt); return Task.FromResult(Vorbereitet); },
                v => { angewandt.Add(v); return Task.FromResult("verworfen"); },
                "Vom Server laden");

        [TestMethod]
        public async Task NoThenYesDiscardsInsteadOfSending()
        {
            var prompt = new AntwortFolge(false, true);
            UserPrompt.Current = prompt;
            var aufrufe = new List<MassDeletion?>();
            var angewandt = new List<MassDeletionDiscard>();
            var vorbereitet = new List<MassDeletion>();

            var ergebnis = await MassDeletionPrompt.RunAskingAsync(Lauf(aufrufe), "Vom Server laden", Ausweg(angewandt, vorbereitet));

            Assert.AreEqual("verworfen", ergebnis);
            Assert.AreEqual(1, aufrufe.Count, "Nach „Nein“ lief der Abgleich noch einmal - mit Freigabe?");
            Assert.AreSame(Gesperrt, vorbereitet.Single(), "Vorbereitet wurde nicht die gemeldete Löschung.");
            Assert.AreSame(Vorbereitet, angewandt.Single());
            Assert.AreEqual(2, prompt.Fragen.Count);
        }

        /// <summary>Die zweite Frage nennt, was zurückkommt, was offen bleibt, und dass nichts hinausgeht.</summary>
        [TestMethod]
        public async Task TheSecondQuestionTellsTheNumbers()
        {
            var prompt = new AntwortFolge(false, true);
            UserPrompt.Current = prompt;

            await MassDeletionPrompt.RunAskingAsync(Lauf([]), "Vom Server laden", Ausweg([], []));

            var frage = prompt.Fragen[1];
            StringAssert.Contains(frage, "Zurück kommen 12 Fragen und 2 Spiele samt 17 weiteren Einträgen");
            StringAssert.Contains(frage, "Deine 3 anderen offenen Änderungen bleiben erhalten");
            StringAssert.Contains(frage, "Hinausgeschickt wird dabei nichts");
        }

        /// <summary>Zweimal „Nein": es bleibt bei der Sperre wie vor V17, nichts wird angewandt.</summary>
        [TestMethod]
        public async Task NoTwiceKeepsTheGuard()
        {
            UserPrompt.Current = new AntwortFolge(false, false);
            var aufrufe = new List<MassDeletion?>();
            var angewandt = new List<MassDeletionDiscard>();

            await Assert.ThrowsExactlyAsync<SyncSafetyException>(
                () => MassDeletionPrompt.RunAskingAsync(Lauf(aufrufe), "Anmeldung", Ausweg(angewandt, [])));

            Assert.AreEqual(1, aufrufe.Count);
            Assert.AreEqual(0, angewandt.Count, "Trotz „Nein“ verworfen.");
        }

        /// <summary>Bei Ja zur Freigabe wird nicht nach dem Verwerfen gefragt.</summary>
        [TestMethod]
        public async Task YesToTheDeletionAsksNothingMore()
        {
            var prompt = new AntwortFolge(true);
            UserPrompt.Current = prompt;
            var vorbereitet = new List<MassDeletion>();

            Assert.AreEqual("freigegeben", await MassDeletionPrompt.RunAskingAsync(Lauf([]), "Anmeldung", Ausweg([], vorbereitet)));
            Assert.AreEqual(1, prompt.Fragen.Count);
            Assert.AreEqual(0, vorbereitet.Count, "Nach „Ja“ wurde trotzdem der Serverstand für das Verwerfen geholt.");
        }

        /// <summary>„Jetzt abgleichen": nach „Nein“ der Ausweg; gesendet wird dabei nichts.</summary>
        [TestMethod]
        public async Task SyncNowOffersTheDiscardAfterNo()
        {
            UserPrompt.Current = new AntwortFolge(false, true);
            var gesendet = new List<MassDeletion>();
            var verworfen = new List<MassDeletionDiscard>();

            var erledigt = await MassDeletionPrompt.ApprovePendingAsync(Gesperrt,
                f => { gesendet.Add(f); return Task.CompletedTask; }, "Jetzt abgleichen",
                async g => await Ausweg(verworfen, [])(g) != null);

            Assert.IsTrue(erledigt);
            Assert.AreEqual(0, gesendet.Count, "Nach „Nein“ gesendet.");
            Assert.AreEqual(1, verworfen.Count);
        }

        /// <summary>Kommt auch am Server nichts mehr zurück, sagt die Frage es, statt „Zurück kommen“ ohne Zahl.</summary>
        [TestMethod]
        public void WhenNothingComesBackTheQuestionSaysSo()
        {
            var nichts = new MassDeletionDiscard(Gesperrt, new MassDeletion(0, 0, 20, []), 0, 0);

            var frage = MassDeletionPrompt.DiscardQuestion(nichts);

            StringAssert.Contains(frage, "zurück kommt nichts");
            Assert.IsFalse(frage.Contains("offenen Änderung"), "Ohne offene Änderungen nennt die Frage welche.");
        }
    }
}
