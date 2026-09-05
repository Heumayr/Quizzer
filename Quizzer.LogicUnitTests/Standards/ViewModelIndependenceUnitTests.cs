using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quizzer.LogicUnitTests.Standards
{
    /// <summary>
    /// Ein ViewModel öffnet kein Fenster selbst.
    /// <para>
    /// Rückfragen und Hinweise laufen über <c>UserPrompt</c>. Der Grund ist nicht Reinheit,
    /// sondern Messbarkeit: ein <c>MessageBox.Show</c> blockiert jeden Testlauf, bis jemand
    /// klickt - und weil niemand klickt, blockiert es bis zum Zeitablauf. Genau deshalb war
    /// <c>GameMasterViewModel</c> bis 2026-09-06 ohne eine einzige Zusicherung: sein Einstieg
    /// <c>LoadModel</c> zeigte vier Meldungen direkt an.
    /// </para>
    /// </summary>
    [TestClass]
    public class ViewModelIndependenceUnitTests
    {
        private static string RepoRoot()
        {
            var verzeichnis = new DirectoryInfo(AppContext.BaseDirectory);

            while (verzeichnis != null && !File.Exists(Path.Combine(verzeichnis.FullName, "Quizzer.slnx")))
                verzeichnis = verzeichnis.Parent;

            Assert.IsNotNull(verzeichnis, "Quizzer.slnx nicht gefunden.");

            return verzeichnis!.FullName;
        }

        /// <summary>
        /// Die ViewModels der Anwendung. <c>UserPrompt.cs</c> ist die Naht selbst und darf.
        /// </summary>
        private static List<string> ViewModelDateien(string root) =>
            Directory.EnumerateFiles(Path.Combine(root, "Quizzer"), "*ViewModel*.cs", SearchOption.AllDirectories)
                .Where(d => !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !d.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .ToList();

        /// <summary>Kein ViewModel zeigt selbst ein Meldungsfenster.</summary>
        [TestMethod]
        public void NoViewModelOpensAMessageBoxItself()
        {
            var root = RepoRoot();
            var funde = new List<string>();

            foreach (var datei in ViewModelDateien(root))
            {
                var zeilen = File.ReadAllLines(datei);

                for (var i = 0; i < zeilen.Length; i++)
                {
                    var roh = zeilen[i].TrimStart();

                    // Auskommentiertes zaehlt nicht - es zeigt nichts an.
                    if (roh.StartsWith("//") || roh.StartsWith('*') || roh.StartsWith("/*"))
                        continue;

                    if (roh.Contains("MessageBox.Show", StringComparison.Ordinal))
                        funde.Add($"{Path.GetRelativePath(root, datei)}:{i + 1}");
                }
            }

            Assert.AreEqual(0, funde.Count,
                "Diese ViewModels zeigen selbst ein Meldungsfenster an - über UserPrompt "
                + "wären sie prüfbar: " + string.Join(", ", funde));
        }

        /// <summary>
        /// Die Gegenrichtung: der Durchlauf findet wirklich ViewModels, und sie benutzen die
        /// Naht auch. Ohne das wäre grün nicht von blind zu unterscheiden.
        /// </summary>
        [TestMethod]
        public void TheScanFindsTheViewModelsAndTheirPrompts()
        {
            var root = RepoRoot();
            var dateien = ViewModelDateien(root);

            Assert.IsTrue(dateien.Count > 15,
                $"Nur {dateien.Count} ViewModel-Dateien gefunden - der Durchlauf greift ins Leere.");

            var mitNaht = dateien.Count(d =>
                File.ReadAllText(d).Contains("UserPrompt.", StringComparison.Ordinal));

            Assert.IsTrue(mitNaht >= 3,
                $"Nur {mitNaht} ViewModels benutzen UserPrompt - dann prüft der Test oben "
                + "etwas, das es gar nicht gibt.");
        }
    }
}
