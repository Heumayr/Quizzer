using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels;
using Quizzer.Logic.Context;
using Quizzer.Logic.Controller;

// Alles Wesentliche ist prozessweit: Settings, die Zufallsquelle und eine einzige
// Testdatenbank. Parallele Testklassen wuerden sich gegenseitig den Boden wegziehen,
// und das saehe aus wie ein Fehler im Programm.
[assembly: DoNotParallelize]

namespace Quizzer.LogicUnitTests
{
    /// <summary>
    /// Baut die Testdatenbank einmal je Testlauf neu auf.
    /// </summary>
    [TestClass]
    public static class TestDatabase
    {
        /// <summary>
        /// Verbindung zur Testdatenbank. Bewusst hier im Quelltext und nicht nur in der
        /// appsettings.json - so steht der Name auch dann fest, wenn die mitkopierte
        /// Konfiguration von Quizzer.DataModels gewinnen sollte.
        /// <para>
        /// Jedes Testprojekt hat eine EIGENE Datenbank: DoNotParallelize wirkt nur innerhalb
        /// einer Assembly, die Projekte laufen aber als eigene Prozesse nebeneinander. Auf einer
        /// gemeinsamen Datenbank loescht dann der eine, waehrend der andere die Migrationssperre
        /// haelt.
        /// </para>
        /// </summary>
        public const string ConnectionString =
            @"Data Source=(localdb)\MSSQLLocalDB;Database=Quizzer_Logic_Tests;Integrated Security=True";

        /// <summary>Controller, die mit ungespeicherten Aenderungen entsorgt wurden.</summary>
        public static List<string> DiscardedChanges { get; } = new();

        [AssemblyInitialize]
        public static void Initialize(TestContext _)
        {
            // Sperre 2: vor dem ersten DataContext setzen. Dessen statischer Konstruktor
            // ruft LoadSettings() nur, solange die Zeichenfolge leer ist - danach greift
            // die mitkopierte appsettings.json gar nicht mehr.
            Settings.ConnectionString = ConnectionString;
            Settings.FilePathQuizzer = Path.Combine(Path.GetTempPath(), "QuizzerTestAssets");

            // Ein vergessenes SaveChangesAsync verliert die Aenderung ohne jeden Fehler. Im
            // Testlauf wird daraus eine Liste, die ein Test nachsehen kann.
            UnsavedChangesWatch.Handler = (controller, anzahl) =>
            {
                lock (DiscardedChanges)
                {
                    DiscardedChanges.Add($"{controller}: {anzahl}");
                }
            };

            // Sperre 1 sitzt in RecreateForTests und wirft, wenn das hier je danebengeht.
            DatabaseInitializer.RecreateForTests();
        }

        [AssemblyCleanup]
        public static void Cleanup() => UnsavedChangesWatch.ResetHandler();

        /// <summary>Vergisst, was bisher verworfen wurde - fuer Tests, die es absichtlich tun.</summary>
        public static void ClearDiscardedChanges()
        {
            lock (DiscardedChanges)
            {
                DiscardedChanges.Clear();
            }
        }

        /// <summary>
        /// Wirft, wenn seit dem letzten Zuruecksetzen ein Controller Aenderungen weggeworfen hat.
        /// </summary>
        public static void ThrowIfChangesWereDiscarded()
        {
            lock (DiscardedChanges)
            {
                if (DiscardedChanges.Count == 0)
                    return;

                var offen = string.Join(", ", DiscardedChanges);
                DiscardedChanges.Clear();

                throw new AssertFailedException(
                    "Ein Controller wurde mit ungespeicherten Aenderungen entsorgt - es fehlt ein "
                    + $"SaveChangesAsync: {offen}");
            }
        }
    }
}
