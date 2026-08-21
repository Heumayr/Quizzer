using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels;
using Quizzer.Logic.Context;

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
        /// </summary>
        public const string ConnectionString =
            @"Data Source=(localdb)\MSSQLLocalDB;Database=Quizzer_Tests;Integrated Security=True";

        [AssemblyInitialize]
        public static void Initialize(TestContext _)
        {
            // Sperre 2: vor dem ersten DataContext setzen. Dessen statischer Konstruktor
            // ruft LoadSettings() nur, solange die Zeichenfolge leer ist - danach greift
            // die mitkopierte appsettings.json gar nicht mehr.
            Settings.ConnectionString = ConnectionString;
            Settings.FilePathQuizzer = Path.Combine(Path.GetTempPath(), "QuizzerTestAssets");

            // Sperre 1 sitzt in RecreateForTests und wirft, wenn das hier je danebengeht.
            DatabaseInitializer.RecreateForTests();
        }
    }
}
