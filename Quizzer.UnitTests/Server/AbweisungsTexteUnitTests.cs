using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Server;

namespace Quizzer.UnitTests.Server
{
    /// <summary>
    /// Was „N Änderungen nicht übertragen - warum?" zu einem Konflikt sagt (V12): beim gleichzeitigen Erstanlegen
    /// derselben festen Id „schon angelegt" mit dem Rat abzuholen, sonst wie bisher „inzwischen geändert".
    /// </summary>
    [TestClass]
    public class AbweisungsTexteUnitTests
    {
        [TestMethod]
        public void AConflictOnARowNeverPulledSaysItIsAlreadyThereAndToFetchIt()
        {
            var (grund, hilfe) = SyncRejectionsViewModel.Erklaere(SyncRejectReason.Conflict, erstAnlage: true);

            Assert.AreEqual("Am Server schon angelegt", grund);
            StringAssert.Contains(hilfe, "Serverstand übernehmen", "Der Rat nennt den Knopf zum Abholen nicht.");
            Assert.IsFalse(hilfe.Contains("noch einmal machen"), "Der Rat verlangt, das Anlegen zu wiederholen.");
        }

        /// <summary>Die Gegenrichtung: ein echter Stand-Konflikt und jeder andere Grund bleiben, wie sie waren.</summary>
        [TestMethod]
        public void EveryOtherCaseKeepsItsText()
        {
            Assert.AreEqual("Am Server inzwischen geändert", SyncRejectionsViewModel.Erklaere(SyncRejectReason.Conflict).Grund);
            Assert.AreEqual("Keine Berechtigung", SyncRejectionsViewModel.Erklaere(SyncRejectReason.Forbidden, erstAnlage: true).Grund);
            Assert.AreEqual("Vom Server als fehlerhaft abgewiesen", SyncRejectionsViewModel.Erklaere(SyncRejectReason.Invalid, erstAnlage: true).Grund);
        }
    }
}
