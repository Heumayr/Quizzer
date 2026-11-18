using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Windows;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// M37 P6, P4-3 - gemessen, widerlegt: an einer gewöhnlichen Zelle ist „getrennt" kein Grund der Antwortsperre (anders als
    /// beim Sprecher einer Teamregel, R5-7). „Joker-Sperre aufheben" lässt dort also keinen Getrennt-Grund fallen, sondern stellt
    /// den Stand ohne Joker her: alle dürfen antworten.
    /// </summary>
    public partial class JokerRundeUnitTests
    {
        [TestMethod]
        public async Task LiftingTheJokerBlockAtAnOrdinaryCellDropsNoDisconnectReason()
        {
            Vergib((1, JokerKind.Block));
            var vm = await OeffneAsync(CellSpecialRule.None);

            await phones[0].DisposeAsync();
            await phones[2].DisposeAsync();
            Assert.IsTrue(await WarteAsync(() => world!.Players[0].ConnectionState == PlayerConnection.Disconnected
                && world.Players[2].ConnectionState == PlayerConnection.Disconnected), "Annas und Claras Telefon trennen nicht.");

            await JokerAsync(1, joker[1].Id, world!.Players[0].Id);
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Die Joker-Runde schliesst nicht.");

            var sperre = serverVm!.BuzzerControlsViewModel!.BuzzerController!.StateManager.AnswerLock;
            CollectionAssert.AreEquivalent(new[] { world.Players[1].Id, world.Players[2].Id }, sperre.Answerers!.ToArray(),
                "Die getrennte Clara ist ausgeschlossen - dann gibt es hier doch einen Getrennt-Grund, und P4-3 trifft zu.");

            await vm.HebeJokerSperreAufAsync();

            Assert.IsNull(sperre.Answerers, "Nach dem Aufheben steht nicht der Stand ohne Joker (alle dürfen antworten).");
            Assert.AreEqual(Visibility.Collapsed, vm.JokerSperreVisibility);
            TestEnvironment.ThrowIfAnythingWasSwallowed();
        }
    }
}
