using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Logic.Context;

namespace Quizzer.LogicUnitTests.Logic.Context
{
    /// <summary>
    /// Die Sperre vor dem Loeschen. Diese Tests sind der einzige Grund, warum man dem
    /// Testlauf zutrauen darf, die Produktivdatenbank stehen zu lassen.
    /// </summary>
    [TestClass]
    public class DatabaseInitializerUnitTests
    {
        private const string LocalDbSource = @"Data Source=(localdb)\MSSQLLocalDB;";

        [TestMethod]
        public void GuardIsTestDatabase_AcceptsLocalTestDatabase()
        {
            DatabaseInitializer.GuardIsTestDatabase(
                LocalDbSource + "Database=Quizzer_Tests;Integrated Security=True");
        }

        [TestMethod]
        public void GuardIsTestDatabase_RejectsProductionDatabase()
        {
            var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
                DatabaseInitializer.GuardIsTestDatabase(
                    LocalDbSource + "Database=Quizzer;Integrated Security=True"));

            StringAssert.Contains(ex.Message, "Produktivdatenbank");
        }

        [TestMethod]
        [DataRow("Quizzer")]
        [DataRow("quizzer")]
        [DataRow("QUIZZER")]
        public void GuardIsTestDatabase_RejectsProductionDatabase_RegardlessOfCasing(string name)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                DatabaseInitializer.GuardIsTestDatabase(
                    LocalDbSource + $"Database={name};Integrated Security=True"));
        }

        [TestMethod]
        [DataRow("Produktion")]
        [DataRow("Quizzer_Test")]
        [DataRow("Quizzer_tests")]
        [DataRow("Tests_Quizzer")]
        public void GuardIsTestDatabase_RejectsAnythingNotEndingInTheTestSuffix(string name)
        {
            var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
                DatabaseInitializer.GuardIsTestDatabase(
                    LocalDbSource + $"Database={name};Integrated Security=True"));

            StringAssert.Contains(ex.Message, "_Tests");
        }

        [TestMethod]
        public void GuardIsTestDatabase_RejectsRemoteServer()
        {
            var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
                DatabaseInitializer.GuardIsTestDatabase(
                    "Data Source=sql01.firma.local;Database=Quizzer_Tests;Integrated Security=True"));

            StringAssert.Contains(ex.Message, "LocalDB");
        }

        /// <summary>
        /// Die Freigabe ueber QUIZZER_LOGIC_TEST_SQL laesst genau den genannten Server zu - keinen
        /// anderen, und auch dort nur Testdatenbanken (2026-10-22).
        /// </summary>
        [TestMethod]
        public void GuardIsTestDatabase_AcceptsOnlyTheExplicitlyNamedTestServer()
        {
            var vorher = Environment.GetEnvironmentVariable(DatabaseInitializer.TestServerVariable);

            try
            {
                Environment.SetEnvironmentVariable(DatabaseInitializer.TestServerVariable,
                    "Server=sqlci,1433;Database=Quizzer_Logic_Tests;User Id=sa;Password=x");

                DatabaseInitializer.GuardIsTestDatabase("Server=sqlci,1433;Database=Quizzer_Logic_Tests;User Id=sa;Password=x");

                Assert.ThrowsExactly<InvalidOperationException>(() =>
                    DatabaseInitializer.GuardIsTestDatabase("Data Source=sql01.firma.local;Database=Quizzer_Tests;Integrated Security=True"),
                    "Ein anderer Server wurde zugelassen.");

                Assert.ThrowsExactly<InvalidOperationException>(() =>
                    DatabaseInitializer.GuardIsTestDatabase("Server=sqlci,1433;Database=Quizzer;User Id=sa;Password=x"),
                    "Auf dem freigegebenen Server wurde eine Nicht-Testdatenbank zugelassen.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(DatabaseInitializer.TestServerVariable, vorher);
            }
        }

        [TestMethod]
        public void GuardIsTestDatabase_RejectsMissingDatabaseName()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                DatabaseInitializer.GuardIsTestDatabase(LocalDbSource));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void GuardIsTestDatabase_RejectsEmptyConnectionString(string? connectionString)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                DatabaseInitializer.GuardIsTestDatabase(connectionString));
        }
    }
}
