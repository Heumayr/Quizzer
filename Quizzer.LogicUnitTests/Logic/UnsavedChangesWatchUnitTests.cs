using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic
{
    /// <summary>
    /// Der Riegel gegen die teuerste Falle des Schreibpfads: <c>UpsertAsync</c> und die anderen
    /// Schreibmethoden schreiben nichts von selbst. Wer <c>SaveChangesAsync</c> vergisst, verliert
    /// die Aenderung beim Entsorgen des Controllers - ohne Fehler, ohne Meldung.
    /// </summary>
    [TestClass]
    public class UnsavedChangesWatchUnitTests
    {
        private Action<string, int> vorheriger = null!;
        private readonly List<string> gemeldet = new();

        [TestInitialize]
        public void SetUp()
        {
            vorheriger = UnsavedChangesWatch.Handler;
            gemeldet.Clear();

            UnsavedChangesWatch.Handler = (controller, anzahl) => gemeldet.Add($"{controller}:{anzahl}");
        }

        [TestCleanup]
        public void TearDown()
        {
            UnsavedChangesWatch.Handler = vorheriger;
            TestDatabase.ClearDiscardedChanges();
        }

        /// <summary>Der Regelfall: wer speichert, verliert nichts und wird nicht gemeldet.</summary>
        [TestMethod]
        public async Task SavingLeavesNothingBehind()
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Wach-Test gespeichert" };

            using (var ctrl = new CategoriesController())
            {
                await ctrl.UpsertAsync(kategorie);
                await ctrl.SaveChangesAsync();
            }

            CollectionAssert.AreEqual(Array.Empty<string>(), gemeldet,
                "Ein sauber gespeicherter Vorgang darf nicht gemeldet werden: "
                + string.Join(", ", gemeldet));

            using (var ctrl = new CategoriesController())
            {
                await ctrl.DeleteAsync(kategorie.Id);
                await ctrl.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Die Gegenrichtung, und dafuer gibt es den Riegel: ohne <c>SaveChangesAsync</c> ist die
        /// Aenderung weg, und das muss auffallen.
        /// </summary>
        [TestMethod]
        public async Task ForgettingToSaveIsReported()
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Wach-Test vergessen" };

            using (var ctrl = new CategoriesController())
            {
                await ctrl.UpsertAsync(kategorie);
                // Absichtlich kein SaveChangesAsync.
            }

            Assert.AreEqual(1, gemeldet.Count,
                "Die verworfene Aenderung wurde nicht gemeldet - die Falle bliebe unsichtbar.");

            StringAssert.StartsWith(gemeldet[0], nameof(CategoriesController),
                "Die Meldung nennt nicht, welcher Controller betroffen war.");

            // Und der Beweis, dass wirklich nichts geschrieben wurde.
            using var pruefer = new CategoriesController();
            var geladen = await pruefer.GetAsync(kategorie.Id);

            Assert.IsNull(geladen, "Die Kategorie steht in der Datenbank - dann misst der Test das Falsche.");
        }

        /// <summary>
        /// Ein Controller, der sich den Kontext eines anderen teilt, meldet nicht selbst - sonst
        /// zeigte jede Transaktionsklammer eine Falschmeldung, waehrend der Eigentuemer noch
        /// speichert.
        /// </summary>
        [TestMethod]
        public async Task ASharedContextIsReportedByItsOwnerOnly()
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Wach-Test geteilt" };

            using (var eigentuemer = new CategoriesController())
            {
                using (var geteilt = new CategoriesController(eigentuemer))
                {
                    await geteilt.UpsertAsync(kategorie);
                }

                Assert.AreEqual(0, gemeldet.Count,
                    "Der geteilte Controller hat gemeldet, obwohl der Eigentuemer noch speichern kann.");

                await eigentuemer.SaveChangesAsync();
            }

            Assert.AreEqual(0, gemeldet.Count,
                "Nach dem Speichern durch den Eigentuemer darf nichts gemeldet werden.");

            using (var ctrl = new CategoriesController())
            {
                await ctrl.DeleteAsync(kategorie.Id);
                await ctrl.SaveChangesAsync();
            }
        }
    }
}
