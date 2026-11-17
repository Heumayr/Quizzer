using LocalBuzzer.Service.Base.States;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base.States
{
    /// <summary>
    /// Missbrauch an der Eingabe (M37 E4): beim Finale gibt jeder genau einmal ab - eine zweite Abgabe wird verworfen und das
    /// Telefon bleibt gesperrt -, und kein Text ist länger als <see cref="BuzzerInputState.MaxLength"/> Zeichen.
    /// </summary>
    public partial class BuzzerInputStateUnitTests
    {
        [TestMethod]
        public void InTheFinalASecondAnswerIsDiscardedAndThePhoneStaysLocked()
        {
            var (state, players) = StateWith(2);
            state.EinmalAbgabe = true;
            state.Reset();

            Assert.IsTrue(state.SetInput(Input(players[0], "Ottawa")));
            Assert.IsFalse(state.SetInput(Input(players[0], "Toronto")), "Die zweite Abgabe wurde angenommen.");

            Assert.AreEqual("Ottawa", state.InputsForPlayer[players[0].Id].Value, "Die erste Antwort wurde ersetzt.");
            Assert.IsTrue(state.IsLockedFor(players[0]), "Wer abgegeben hat, kann weiter eingeben.");
            Assert.IsFalse(state.IsLockedFor(players[1]), "Wer noch nicht abgegeben hat, ist gesperrt.");
            Assert.IsFalse(state.Locked, "Die Runde schließt vor der zweiten Antwort.");
        }

        /// <summary>Eine leere Eingabe ist keine Abgabe - sie sperrt auch beim Finale nicht.</summary>
        [TestMethod]
        public void InTheFinalAnEmptyInputDoesNotUseTheOneAnswer()
        {
            var (state, players) = StateWith(2);
            state.EinmalAbgabe = true;
            state.Reset();

            Assert.IsTrue(state.SetInput(Input(players[0], "  ")));
            Assert.IsFalse(state.IsLockedFor(players[0]), "Eine leere Eingabe hat die eine Antwort verbraucht.");
            Assert.IsTrue(state.SetInput(Input(players[0], "Ottawa")));
            Assert.AreEqual("Ottawa", state.InputsForPlayer[players[0].Id].Value);
        }

        /// <summary>Ohne Finale bleibt es wie immer: ein zweiter Tipp ersetzt den ersten, das nächste Layout räumt die Einmal-Regel weg.</summary>
        [TestMethod]
        public void WithoutTheFinalASecondGuessReplacesTheFirst()
        {
            var (state, players) = StateWith(2);
            state.EinmalAbgabe = true;
            state.ClearAndLock();
            state.Reset();

            Assert.IsFalse(state.EinmalAbgabe, "Die Einmal-Regel überlebt das nächste Layout.");
            state.SetInput(Input(players[0], "100"));
            Assert.IsTrue(state.SetInput(Input(players[0], "250")));
            Assert.AreEqual("250", state.InputsForPlayer[players[0].Id].Value);
            Assert.IsFalse(state.IsLockedFor(players[0]));
        }

        /// <summary>Ein 30-KB-Text (knapp unter der SignalR-Grenze) wird auf 80 Zeichen gekürzt, bevor ihn jemand liest.</summary>
        [TestMethod]
        public void ALongInputIsCutToTheLimit()
        {
            var (state, players) = StateWith(2);
            state.Reset();
            var lang = new string('x', 30_000);

            state.SetInput(Input(players[0], lang));

            Assert.AreEqual(BuzzerInputState.MaxLength, state.InputsForPlayer[players[0].Id].Value.Length);
            Assert.AreEqual(80, BuzzerInputState.MaxLength, "Die Grenze steht nicht mehr auf 80 - inputLayout.js nachziehen.");
        }

        [TestMethod]
        public void CuttingKeepsShortTextsAndNeverSplitsAnEmoji()
        {
            Assert.AreEqual("Ottawa", BuzzerInputState.Kuerze("Ottawa"));
            Assert.AreEqual(string.Empty, BuzzerInputState.Kuerze(null));
            Assert.AreEqual(new string('a', 80), BuzzerInputState.Kuerze(new string('a', 81)));

            // 79 Buchstaben, dann ein Emoji aus zwei Hälften: die erste Hälfte allein bliebe kein Zeichen.
            var text = new string('a', 79) + "😀" + "b";
            var gekuerzt = BuzzerInputState.Kuerze(text);

            Assert.AreEqual(new string('a', 79), gekuerzt);
            Assert.IsFalse(char.IsHighSurrogate(gekuerzt[^1]));
        }
    }
}
