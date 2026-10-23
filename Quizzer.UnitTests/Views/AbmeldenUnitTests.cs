using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.Views;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Abmelden vom Quizzer-Server: Rückfrage, dann Neustart ins Anmeldefenster.
    /// <para>
    /// <b>Nutzerwort vom 2026-10-23 (M28):</b> „fix an und Abmeldung sinnvolles handling im der
    /// wpf". Bis dahin führte kein Weg vom einmal angemeldeten Konto weg - ab dem zweiten Start
    /// meldete sich das Programm sogar selbst wieder an.
    /// </para>
    /// <para>
    /// Geprüft wird der Ablauf des Befehls über <c>ExecuteAsync</c>, das <c>CanExecute</c>
    /// umgeht - eine echte Serversitzung lässt sich im Testlauf nicht herstellen.
    /// </para>
    /// </summary>
    [TestClass]
    public class AbmeldenUnitTests
    {
        private int neustarts;

        [TestInitialize]
        public void SetUp()
        {
            neustarts = 0;
            AppRestart.Current = () => neustarts++;
        }

        [TestCleanup]
        public void TearDown()
        {
            AppRestart.Reset();
            UserPrompt.Reset();
        }

        /// <summary>Ohne Serversitzung gibt es nichts abzumelden - der Knopf ist dann gesperrt.</summary>
        [TestMethod]
        public void WithoutAServerSessionSignOutIsNotOffered()
        {
            Assert.IsFalse(new MainViewModel().LogoutCommand.CanExecute(null));
        }

        /// <summary>Wer die Rückfrage verneint, bleibt angemeldet - kein Neustart.</summary>
        [TestMethod]
        public async Task DecliningKeepsTheSession()
        {
            var prompt = new RecordingUserPrompt(answer: false);
            UserPrompt.Current = prompt;

            await ((AsyncRelayCommand)new MainViewModel().LogoutCommand).ExecuteAsync(null);

            Assert.AreEqual(1, prompt.Confirms.Count, "Abgemeldet wurde ohne Rückfrage.");
            StringAssert.Contains(prompt.Confirms[0].Message, "neu starten",
                "Die Rückfrage sagt nicht, dass das Programm neu startet: " + prompt.Confirms[0].Message);
            Assert.AreEqual(0, neustarts, "Trotz „Nein“ neu gestartet.");
        }

        /// <summary>Bestätigt: genau ein Neustart.</summary>
        [TestMethod]
        public async Task ConfirmingRestartsIntoTheSignIn()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);

            await ((AsyncRelayCommand)new MainViewModel().LogoutCommand).ExecuteAsync(null);

            Assert.AreEqual(1, neustarts, "Nach dem Abmelden startete das Programm nicht neu.");
        }
    }
}
