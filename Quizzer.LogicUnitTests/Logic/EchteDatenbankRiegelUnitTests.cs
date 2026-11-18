using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Logic.Demo;
using System.Diagnostics;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.Logic
{
    /// <summary>
    /// Der Riegel vor den Demobefehlen von <c>Quizzer.EF</c> (M37 P1, V29): ohne <c>appsettings.json</c> gilt die Vorgabe, die
    /// echte Spieldatenbank <c>Quizzer</c> - jedes <c>demo-*</c> (auch <c>demo-entfernen</c>) träfe den Bestand. Gestartet
    /// wird das gebaute Programm in einer Kopie, deren <c>appsettings.json</c> auf eine <b>Attrappe</b> zeigt: dieselbe
    /// Datenbank „Quizzer“, aber ein Server, den es nicht gibt. Ginge der Riegel nicht, scheiterte der Befehl an der
    /// Verbindung - die echte Datenbank erreicht dieser Test auf keinem Weg.
    /// </summary>
    [TestClass]
    public class EchteDatenbankRiegelUnitTests
    {
        private const string Attrappe = @"Data Source=127.0.0.1,1;Database=Quizzer;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        private static string kopie = string.Empty;

        [ClassInitialize]
        public static void KopiereProgramm(TestContext _)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Quizzer.slnx")))
                root = root.Parent;

            Assert.IsNotNull(root, "Quizzer.slnx nicht gefunden.");

            // Dieselbe Konfiguration wie dieser Testlauf: ...\bin\<Debug|Release>\net10.0\
            var konfiguration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
            var quelle = Path.Combine(root!.FullName, "Quizzer.EF", "bin", konfiguration, "net10.0");
            Assert.IsTrue(File.Exists(Path.Combine(quelle, "Quizzer.EF.dll")), $"Quizzer.EF ist nicht gebaut: {quelle}");

            kopie = Directory.CreateTempSubdirectory("quizzer-ef-riegel-").FullName;

            foreach (var datei in Directory.GetFiles(quelle, "*", SearchOption.AllDirectories))
            {
                var ziel = Path.Combine(kopie, Path.GetRelativePath(quelle, datei));
                Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
                File.Copy(datei, ziel);
            }

            // Ersetzt eine mitgebaute appsettings.json - nur die Attrappe zählt.
            File.WriteAllText(Path.Combine(kopie, "appsettings.json"), JsonSerializer.Serialize(new
            {
                AppSettings = new { MsSqlConString = Attrappe, FilePathQuizzer = Path.Combine(kopie, "Daten") },
            }));
        }

        [ClassCleanup]
        public static void RaeumeAuf()
        {
            if (kopie.Length > 0 && Directory.Exists(kopie))
                Directory.Delete(kopie, recursive: true);
        }

        private static (int Code, string Ausgabe, string Fehler) Starte(params string[] args)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = kopie,
            };
            start.ArgumentList.Add(Path.Combine(kopie, "Quizzer.EF.dll"));
            foreach (var arg in args)
                start.ArgumentList.Add(arg);

            using var lauf = Process.Start(start);
            Assert.IsNotNull(lauf, "Quizzer.EF liess sich nicht starten.");

            var ausgabe = lauf!.StandardOutput.ReadToEndAsync();
            var fehler = lauf.StandardError.ReadToEndAsync();
            Assert.IsTrue(lauf.WaitForExit(60_000), "Quizzer.EF hängt.");

            return (lauf.ExitCode, ausgabe.Result, fehler.Result);
        }

        /// <summary>Die Demobefehle, wie die Hilfe sie nennt - ein neuer ist ohne weiteres dabei.</summary>
        private static List<string> Demobefehle()
            => [.. Starte().Ausgabe.Split('\n').Select(z => z.Trim().Split(' ')[0]).Where(w => w.StartsWith("demo-", StringComparison.Ordinal))];

        [TestMethod]
        public void EveryDemoCommandStopsAtADatabaseNamedQuizzerWithoutConfirmation()
        {
            var befehle = Demobefehle();
            CollectionAssert.IsSubsetOf(new[] { "demo-anlegen", "demo-entfernen", "demo-vorrat" }, befehle, $"Die Hilfe nennt: {string.Join(", ", befehle)}");

            foreach (var befehl in befehle)
            {
                var (code, ausgabe, fehler) = Starte(befehl);

                StringAssert.Contains(ausgabe, "127.0.0.1,1", $"{befehl}: die Attrappe galt nicht - Ausgabe: {ausgabe}");
                Assert.AreEqual(2, code, $"{befehl} lief gegen „Quizzer“ los (Code {code}): {ausgabe}{fehler}");
                StringAssert.Contains(fehler, "--echte-datenbank", $"{befehl}: die Meldung nennt den Schalter nicht.");
            }
        }

        /// <summary>Wann der Riegel greift: nur bei <c>demo-*</c>, nur bei „Quizzer“ (auch unlesbar = Vorgabe), nie mit Schalter.</summary>
        [TestMethod]
        public void TheGuardStopsOnlyDemoCommandsOnQuizzerWithoutTheSwitch()
        {
            const string vorgabe = @"Data Source=(localdb)\MSSQLLocalDB;Database=Quizzer;Integrated Security=True";

            StringAssert.Contains(EchteDatenbankRiegel.Pruefe(["demo-entfernen"], vorgabe), EchteDatenbankRiegel.Schalter);
            Assert.IsNotNull(EchteDatenbankRiegel.Pruefe(["demo-vorrat"], @"Server=.\SQLEXPRESS;Initial Catalog=quizzer"), "Groß-/Kleinschreibung umging den Riegel.");
            Assert.IsNotNull(EchteDatenbankRiegel.Pruefe(["demo-anlegen"], "das ist keine Verbindung"), "Eine unlesbare Verbindung umging den Riegel.");
            Assert.IsNotNull(EchteDatenbankRiegel.Pruefe(["demo-anlegen", "--andere"], vorgabe), "Ein fremder Schalter galt als Bestätigung.");

            Assert.IsNull(EchteDatenbankRiegel.Pruefe(["demo-entfernen", EchteDatenbankRiegel.Schalter], vorgabe), "Mit Schalter abgewiesen.");
            Assert.IsNull(EchteDatenbankRiegel.Pruefe(["demo-entfernen"], @"Data Source=(localdb)\MSSQLLocalDB;Database=Quizzer_Logic_Tests"), "Eine andere Datenbank abgewiesen.");
            Assert.IsNull(EchteDatenbankRiegel.Pruefe([], vorgabe), "Die Hilfe abgewiesen.");
            Assert.IsNull(EchteDatenbankRiegel.Pruefe(["migrations"], vorgabe), "Ein Befehl ohne demo- abgewiesen.");
        }
    }
}
