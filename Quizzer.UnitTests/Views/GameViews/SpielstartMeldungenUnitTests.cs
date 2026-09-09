using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.Views.GameViews;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// <b>Was der Spielleiter beim Start zu lesen bekommt.</b>
    /// <para>
    /// <b>Der Riegel hatte bis zum 2026-09-09 keine einzige Zusicherung</b> - und er ist das
    /// Letzte, was zwischen einer kaputten Frage und den Gästen steht.
    /// </para>
    /// <para>
    /// <b>Bis dahin filterte er auf <c>IsError</c></b>, und alles Schwächere blieb ungesagt.
    /// Gemessen an der Spieldatenbank: drei Warnungen im ganzen Bestand, der Demo-Abend trägt
    /// keine - die Anzeige kostet also nichts.
    /// </para>
    /// </summary>
    [TestClass]
    public class SpielstartMeldungenUnitTests
    {
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void Setup()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
        }

        [TestCleanup]
        public void Cleanup() => UserPrompt.Reset();

        private string LetzterText => prompt.Confirms[^1].Message;

        /// <summary>
        /// <b>Ohne Befund wird nicht gefragt.</b> Ein Dialog bei jedem Start waere die sicherste
        /// Art, ihn ungelesen wegzuklicken.
        /// </summary>
        [TestMethod]
        public void NothingFoundAsksNothing()
        {
            Assert.IsTrue(GameMasterViewModel.StartFreigegeben([], []));

            Assert.AreEqual(0, prompt.Confirms.Count,
                "Es wurde gefragt, obwohl nichts anlag: " + string.Join(" | ",
                    prompt.Confirms.Select(c => c.Message)));
        }

        /// <summary>Ein Fehler heisst: laesst sich nicht ordentlich spielen.</summary>
        [TestMethod]
        public void AnErrorIsCalledUnplayable()
        {
            GameMasterViewModel.StartFreigegeben(["Test MC: keine Loesung markiert."], []);

            Assert.AreEqual(1, prompt.Confirms.Count, "Es wurde nicht gefragt.");

            StringAssert.Contains(LetzterText, "lässt sich nicht ordentlich spielen",
                "Der Fehlersatz fehlt: " + LetzterText);

            StringAssert.Contains(LetzterText, "Test MC: keine Loesung markiert.");
        }

        /// <summary>
        /// <b>Eine Warnung heisst etwas anderes</b> - und das ist der Punkt der Aenderung.
        /// Stuende sie unter demselben Satz, waere aus jeder Kleinigkeit ein Defekt geworden.
        /// </summary>
        [TestMethod]
        public void AWarningIsNotCalledUnplayable()
        {
            GameMasterViewModel.StartFreigegeben([], ["Hi: Am Ende steht nichts."]);

            Assert.AreEqual(1, prompt.Confirms.Count,
                "Bei einer Warnung wird nicht gefragt - dann erfaehrt er sie nie.");

            StringAssert.Contains(LetzterText, "fällt am Abend vielleicht auf",
                "Der Warnsatz fehlt: " + LetzterText);

            Assert.IsFalse(LetzterText.Contains("lässt sich nicht ordentlich spielen",
                StringComparison.Ordinal),
                "Eine blosse Warnung darf nicht als unspielbar gemeldet werden: " + LetzterText);

            StringAssert.Contains(LetzterText, "Hi: Am Ende steht nichts.");
        }

        /// <summary>Beides zusammen steht in zwei getrennten Blöcken.</summary>
        [TestMethod]
        public void BothAppearSeparately()
        {
            GameMasterViewModel.StartFreigegeben(
                ["Test MC: keine Loesung markiert."],
                ["Hi: Am Ende steht nichts."]);

            var text = LetzterText;

            StringAssert.Contains(text, "lässt sich nicht ordentlich spielen");
            StringAssert.Contains(text, "fällt am Abend vielleicht auf");

            Assert.IsTrue(
                text.IndexOf("Test MC", StringComparison.Ordinal)
                < text.IndexOf("Hi:", StringComparison.Ordinal),
                "Die Fehler gehoeren vor die Warnungen: " + text);
        }

        /// <summary>
        /// Die Mehrzahl wird gezählt, nicht geraten - eine falsche Zahl im Dialog kostet
        /// Vertrauen in den ganzen Riegel.
        /// </summary>
        [TestMethod]
        public void TheCountIsSpelledOut()
        {
            GameMasterViewModel.StartFreigegeben(["A: x", "B: y", "C: z"], []);

            StringAssert.Contains(LetzterText, "3 Fragen im Spielfeld",
                "Die Zahl stimmt nicht: " + LetzterText);
        }

        /// <summary>
        /// <b>Die Antwort des Spielleiters wird durchgereicht</b> - „Nein" muss den Start
        /// wirklich anhalten.
        /// </summary>
        [TestMethod]
        public void SayingNoStopsTheStart()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: false);

            Assert.IsFalse(
                GameMasterViewModel.StartFreigegeben(["Test MC: keine Loesung markiert."], []),
                "Der Start laeuft weiter, obwohl der Spielleiter abgelehnt hat.");
        }
    }
}
