using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.TestShared;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Quizzer.LogicUnitTests.Standards
{
    /// <summary>
    /// Kein Testport doppelt, quer über alle drei Testprojekte (V10). Jeder feste Port steht in
    /// <see cref="TestPorts"/>; geprüft werden die Liste und dass niemand an ihr vorbei einen Port festlegt.
    /// Die Projekte laufen heute nacheinander - ein doppelter Port fiele erst auf, wenn sie es nicht mehr tun.
    /// </summary>
    [TestClass]
    public class TestPortsUnitTests
    {
        private static readonly string[] Testprojekte = ["Quizzer.LogicUnitTests", "Quizzer.UnitTests", "Quizzer.Server.Tests"];

        /// <summary>Liste und Wächter selbst: dort stehen Ports als Probe, nicht als Server.</summary>
        private static readonly string[] NichtGelesen = ["TestPorts.cs", $"{nameof(TestPortsUnitTests)}.cs", $"{nameof(TestServerLoopbackUnitTests)}.cs"];

        /// <summary>Feste Ports außerhalb der Liste, je mit Grund. Ihr Wert darf trotzdem in der Liste nicht vorkommen.</summary>
        private static readonly Dictionary<string, string> Ausnahmen = new()
        {
            [Path.Combine("Quizzer.UnitTests", "Views", "BuzzerViews", "Firewall", "FirewallAssessmentUnitTests.cs")] =
                "5000 ist der Port des Programms, gegen den die Firewallregeln bewertet werden - kein Testserver.",
        };

        private static readonly Regex FesterPort = new(@"\b\w*Port\w*\s*(?:=|=>)\s*(\d{2,5})\b");
        private static readonly Regex Eintrag = new(@"\bTestPorts\s*\.\s*(\w+)");

        private static string RepoRoot()
        {
            var verzeichnis = new DirectoryInfo(AppContext.BaseDirectory);

            while (verzeichnis != null && !File.Exists(Path.Combine(verzeichnis.FullName, "Quizzer.slnx")))
                verzeichnis = verzeichnis.Parent;

            Assert.IsNotNull(verzeichnis, "Quizzer.slnx nicht gefunden.");

            return verzeichnis.FullName;
        }

        internal static List<string> Befunde(
            IReadOnlyDictionary<string, int> liste, IReadOnlyList<(string Datei, string Inhalt)> dateien, IReadOnlyCollection<string> ausnahmen)
        {
            var befunde = liste.GroupBy(e => e.Value).Where(g => g.Count() > 1)
                .Select(g => $"Port {g.Key} steht mehrfach in TestPorts: {string.Join(", ", g.Select(e => e.Key))}")
                .ToList();

            foreach (var name in liste.Keys)
            {
                var nutzer = dateien.Where(d => Eintrag.Matches(d.Inhalt).Any(m => m.Groups[1].Value == name)).Select(d => d.Datei).ToList();
                if (nutzer.Count != 1)
                    befunde.Add($"TestPorts.{name} steht in {nutzer.Count} Dateien: {string.Join(", ", nutzer)}");
            }

            foreach (var (datei, inhalt) in dateien)
            {
                foreach (Match m in FesterPort.Matches(inhalt))
                {
                    var wert = int.Parse(m.Groups[1].Value);
                    if (!ausnahmen.Contains(datei))
                        befunde.Add($"{datei}: fester Port {wert} an TestPorts vorbei");
                    else if (liste.Values.Contains(wert))
                        befunde.Add($"{datei}: Port {wert} der Ausnahme steht auch in TestPorts");
                }
            }

            return befunde;
        }

        [TestMethod]
        public void NoTestPortIsTakenTwice()
        {
            var root = RepoRoot();
            var liste = typeof(TestPorts).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral)
                .ToDictionary(f => f.Name, f => (int)f.GetRawConstantValue()!);
            var dateien = Testprojekte
                .SelectMany(p => Directory.EnumerateFiles(Path.Combine(root, p), "*.cs", SearchOption.AllDirectories))
                .Select(p => Path.GetRelativePath(root, p))
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !NichtGelesen.Contains(Path.GetFileName(p)))
                .Select(p => (Datei: p, Inhalt: File.ReadAllText(Path.Combine(root, p))))
                .ToList();

            // Gegenrichtung: eine leere Liste oder ein Suchlauf, der nichts liest, meldet sonst Erfolg.
            Assert.IsTrue(liste.Count > 20, $"TestPorts hat nur {liste.Count} Einträge - liest die Reflexion die Konstanten noch?");
            foreach (var projekt in Testprojekte)
                Assert.IsTrue(dateien.Count(d => d.Datei.StartsWith(projekt + Path.DirectorySeparatorChar)) > 10, $"{projekt}: kaum Dateien gelesen.");

            var befunde = Befunde(liste, dateien, Ausnahmen.Keys);

            Assert.AreEqual(0, befunde.Count, "Testports:" + Environment.NewLine + string.Join(Environment.NewLine, befunde));
        }

        private static readonly Dictionary<string, int> Probeliste = new() { ["Eins"] = 6001, ["Zwei"] = 6002 };

        private static readonly (string, string)[] SauberesProjekt =
        [
            ("A.cs", "private const int TestPort = TestPorts.Eins;"),
            ("B.cs", "protected override int Port => TestPorts.Zwei;"),
            ("C.cs", "private const int Port = 5000;"),
        ];

        [TestMethod]
        public void ACleanProjectPasses()
            => Assert.AreEqual(0, Befunde(Probeliste, SauberesProjekt, ["C.cs"]).Count, string.Join(" | ", Befunde(Probeliste, SauberesProjekt, ["C.cs"])));

        [TestMethod]
        public void ThePortTwiceInTheListIsFound()
        {
            var befunde = Befunde(new Dictionary<string, int> { ["Eins"] = 6001, ["Zwei"] = 6001 }, SauberesProjekt, ["C.cs"]);

            CollectionAssert.Contains(befunde, "Port 6001 steht mehrfach in TestPorts: Eins, Zwei");
        }

        [TestMethod]
        public void OneEntryInTwoClassesIsFound()
        {
            var befunde = Befunde(Probeliste, [.. SauberesProjekt, ("D.cs", "private const int TestPort = TestPorts.Eins;")], ["C.cs"]);

            CollectionAssert.Contains(befunde, "TestPorts.Eins steht in 2 Dateien: A.cs, D.cs");
        }

        [TestMethod]
        public void AFixedPortBesideTheListIsFound()
        {
            var befunde = Befunde(Probeliste, [.. SauberesProjekt, ("D.cs", "private const int TestPort = 6001;")], ["C.cs"]);

            CollectionAssert.Contains(befunde, "D.cs: fester Port 6001 an TestPorts vorbei");
        }

        [TestMethod]
        public void AnExceptionMustNotShareItsPortWithTheList()
        {
            var befunde = Befunde(new Dictionary<string, int> { ["Eins"] = 5000, ["Zwei"] = 6002 }, SauberesProjekt, ["C.cs"]);

            CollectionAssert.Contains(befunde, "C.cs: Port 5000 der Ausnahme steht auch in TestPorts");
        }
    }
}
