using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using System.IO;
using System.Security.Cryptography;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// P12-3 (Durchsicht M37, P13): ein Testlauf lässt die windowPlacements.json des Nutzers in Ruhe. Gemessen vor der Behebung:
    /// <c>MeldungsfensterUnitTests</c> allein verschob den gemerkten Platz von <c>PromptView</c> in der echten Datei, weil jedes
    /// gezeigte und geschlossene <see cref="WindowBase"/> beim Schließen dorthin schrieb.
    /// </summary>
    [TestClass]
    public class WindowPlacementStoreUnitTests
    {
        private static string EchteDatei => Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Quizzer", "windowPlacements.json"));

        /// <summary>Zeitstempel, Länge und Inhalt der Datei - oder dass sie fehlt.</summary>
        private static string Stand(string datei) => File.Exists(datei)
            ? $"{File.GetLastWriteTimeUtc(datei):o} {new FileInfo(datei).Length} {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(datei)))}"
            : "fehlt";

        [TestMethod]
        public void TheTestRunKeepsItsWindowPlacementsInAThrowawayFolder()
        {
            var ablage = Path.GetFullPath(WindowPlacementStore.Shared.FilePath);

            Assert.AreNotEqual(EchteDatei, ablage, "Die Testfenster schreiben in die windowPlacements.json des Nutzers.");
            StringAssert.StartsWith(ablage, Path.GetFullPath(TestEnvironment.FensterplatzOrdner),
                "Die Testfenster schreiben nicht in den Wegwerf-Ordner des Testlaufs.");
        }

        [TestMethod]
        public void AWindowShownAndClosedInATestLeavesTheUsersFileAlone()
        {
            var vorher = Stand(EchteDatei);

            // Eine frische Ablage: in der des Laufs kann ein nie gezeigtes Fenster einen NaN-Platz hinterlassen haben - solange er
            // darin steht, scheitert jedes Speichern still (gemessen), und dieser Test wäre grün, ohne etwas zu messen.
            var ordner = Path.Combine(TestEnvironment.FensterplatzOrdner, "probe-" + Guid.NewGuid().ToString("N"));

            try
            {
                WindowPlacementStore.UseFolderForTests(ordner);

                UiTestHost.Run(() =>
                {
                    var fenster = new WindowBase { Width = 300, Height = 200, Left = -3000, Top = -3000, ShowInTaskbar = false, ShowActivated = false };
                    fenster.Show();
                    fenster.Close();
                });

                Assert.IsTrue(File.Exists(Path.Combine(ordner, "windowPlacements.json")),
                    "Der Fensterplatz kam nicht in der Wegwerf-Ablage an - dann misst der Test nichts.");
                Assert.AreEqual(vorher, Stand(EchteDatei), "Ein Testfenster hat die windowPlacements.json des Nutzers verändert.");
            }
            finally
            {
                WindowPlacementStore.UseFolderForTests(TestEnvironment.FensterplatzOrdner);
            }
        }
    }
}
