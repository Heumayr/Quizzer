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
