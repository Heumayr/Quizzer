using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Getrennt und per Joker gesperrt sind zwei Gründe (R5-7): „Joker-Sperre aufheben" nimmt nur die Joker-Sperre zurück - ein
    /// getrennter Sprecher bleibt draußen, sein Nächster spricht weiter.
    /// </summary>
    public partial class TeamberatungUnitTests
    {
        [TestMethod]
        public async Task LiftingTheJokerBlockDoesNotBringBackADisconnectedSpeaker()
        {
            jokerFuer = (2, JokerKind.Block);
            var vm = await OeffneTeamberatungAsync();
            Assert.IsTrue(vm.VorphaseLaeuft, "Vor der Teamberatung lief keine Joker-Runde.");

            await phones[0].DisposeAsync();
            Assert.IsTrue(await WarteAsync(() => world!.Players[0].ConnectionState == PlayerConnection.Disconnected), "Annas Telefon trennt nicht.");
            await JokerAsync(2, joker!.Id, ziel: Id(0));
            await NachDerJokerRundeAsync(vm);
            CollectionAssert.AreEquivalent(new[] { Id(1), Id(2) }, Hub.AnswerLock.Answerers!.ToArray(), "Die getrennte und gesperrte Anna spricht.");

            await vm.HebeJokerSperreAufAsync();

            CollectionAssert.AreEquivalent(new[] { Id(1), Id(2) }, Hub.AnswerLock.Answerers!.ToArray(),
                "Das Aufheben der Joker-Sperre holt die getrennte Anna als Sprecherin zurück.");
            Assert.AreEqual("Sprecher für Rot", Karte(vm, 1).SonderregelText, "Bert spricht nach dem Aufheben nicht mehr für Rot.");
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }
    }
}
