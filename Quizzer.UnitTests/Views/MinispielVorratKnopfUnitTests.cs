using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Vorrat;
using Quizzer.Views;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Der Knopf „Minispiel-Vorrat anlegen" im Fragenkatalog (M35 C8): ohne Server legt er an, sagt, was geschah, und
    /// zeigt die Fragen gleich in der Liste; ein zweiter Druck ändert nichts und sagt das auch.
    /// </summary>
    [TestClass]
    public class MinispielVorratKnopfUnitTests
    {
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            prompt = new RecordingUserPrompt();
            UserPrompt.Current = prompt;
            TestEnvironment.ClearDiscardedChanges();
            await RaeumeVorratAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await RaeumeVorratAsync();
            UserPrompt.Reset();
            TestEnvironment.ClearDiscardedChanges();
        }

        private static async Task RaeumeVorratAsync()
        {
            using var fragen = new QuestionBasesController();
            using var kategorien = new CategoriesController(fragen);

            foreach (var id in Enum.GetValues<MinigameKind>().Select(MinispielVorrat.IdFor))
            {
                if (await fragen.GetAsync(id) != null)
                    await fragen.DeleteAsync(id);
            }

            if (await kategorien.GetAsync(MinispielVorrat.KategorieId) != null)
                await kategorien.DeleteAsync(MinispielVorrat.KategorieId);

            await fragen.SaveChangesAsync();
        }

        private static List<QuestionBase> VorratInDerListe(QuestionsViewModel vm)
            => vm.Questions.Where(q => q.CategoryId == MinispielVorrat.KategorieId).ToList();

        [TestMethod]
        public async Task ThePressCreatesTheStockShowsItAndASecondPressChangesNothing()
        {
            Assert.IsFalse(Quizzer.Server.ServerSession.IsActive, "Die Probe gilt dem Betrieb ohne Server.");

            var vm = new QuestionsViewModel();
            await vm.LoadForTestAsync();
            var knopf = (AsyncRelayCommand)vm.VorratAnlegenCommand;

            await knopf.ExecuteAsync(null);

            Assert.AreEqual(MinispielVorrat.Arten.Count, VorratInDerListe(vm).Count, "Die Liste zeigt den neuen Vorrat nicht.");
            Assert.AreEqual(1, prompt.Informations.Count, "Der Knopf sagt nicht, was geschah.");
            StringAssert.StartsWith(prompt.Informations[0], $"{MinispielVorrat.Arten.Count} Minispiele angelegt");

            await knopf.ExecuteAsync(null);

            Assert.AreEqual(MinispielVorrat.Arten.Count, VorratInDerListe(vm).Count, "Der zweite Druck hat etwas angelegt.");
            Assert.AreEqual(2, prompt.Informations.Count);
            StringAssert.StartsWith(prompt.Informations[1], "Der Vorrat ist schon vollständig da");
            Assert.AreEqual(0, prompt.Confirmations.Count, "Ohne Server gibt es nichts abzuholen und nichts zu fragen.");
        }
    }
}
