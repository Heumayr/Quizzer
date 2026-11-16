using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Demo;
using Quizzer.Logic.Vorrat;

namespace Quizzer.LogicUnitTests.Logic
{
    /// <summary>
    /// Der Vorrat geteilter Minispiele (M35 C8, V4): genau die inhaltsfreien Arten, geteilt, mit festen Ids, zweimal
    /// angelegt nicht doppelt - und <c>demo-entfernen</c> lässt ihn stehen.
    /// </summary>
    [TestClass]
    public class MinispielVorratUnitTests
    {
        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();
            await RaeumeVorratAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            await DemoDataRemover.RemoveAsync();
            await RaeumeVorratAsync();
            TestDatabase.ClearDiscardedChanges();
        }

        /// <summary>Der Vorrat ist echter Bestand - kein Aufräumweg der Anwendung nimmt ihn mit, also tut es der Test.</summary>
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

        private static async Task<List<MinigameQuestion>> VorratInDerDatenbankAsync()
        {
            using var fragen = new QuestionBasesController();

            return (await fragen.GetAllAsync())
                .OfType<MinigameQuestion>()
                .Where(q => q.CategoryId == MinispielVorrat.KategorieId)
                .ToList();
        }

        /// <summary>
        /// Genau die Arten, die ohne eine einzige Zeile spielbar sind - gemessen an derselben Prüfung wie der Editor,
        /// nicht an den Profilfeldern, aus denen der Vorrat sie liest. Jede Vorratsfrage besteht die Prüfung ohne Befund.
        /// </summary>
        [TestMethod]
        public void TheStockHoldsExactlyTheKindsThatPlayWithoutRows()
        {
            var ohneZeilenSpielbar = MinigameProfiles.All
                .Where(p =>
                {
                    var leer = new MinigameQuestion { CategoryId = Guid.NewGuid() };
                    p.ApplyDefaults(leer);
                    return !MinigameChecks.Content(leer).Any(i => i.Code == MinigameChecks.TooFewOptions);
                })
                .Select(p => p.Kind)
                .ToList();

            CollectionAssert.AreEquivalent(ohneZeilenSpielbar, MinispielVorrat.Arten.ToList(),
                "Der Vorrat enthält nicht genau die inhaltsfreien Arten.");

            var befunde = MinispielVorrat.Arten
                .SelectMany(a => QuestionValidator.Validate(MinispielVorrat.Baue(a, null)).Select(i => $"{a}: {i.Severity} {i.Message}"))
                .ToList();

            Assert.AreEqual(0, befunde.Count, string.Join(Environment.NewLine, befunde));
        }

        /// <summary>
        /// Die Ids sind fest - eine geänderte Ableitung legte auf jedem Konto, das den alten Vorrat schon hat, alles
        /// noch einmal an. Darum steht eine Id hier ausgeschrieben.
        /// </summary>
        [TestMethod]
        public void TheIdsAreFixedAndDistinct()
        {
            Assert.AreEqual(new Guid("c8d0a7f1-3e5b-4c35-9a08-000000000002"), MinispielVorrat.IdFor(MinigameKind.Reaction));
            Assert.AreEqual(new Guid("c8d0a7f1-3e5b-4c35-9a08-000000000000"), MinispielVorrat.KategorieId);

            var ids = Enum.GetValues<MinigameKind>().Select(MinispielVorrat.IdFor).Append(MinispielVorrat.KategorieId).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "Zwei Arten teilen sich eine Id.");
        }

        /// <summary>
        /// Anlegen schreibt jede Art einmal, geteilt, mit fester Id und dem Anleger als Besitzer; ein zweites Anlegen
        /// ändert nichts - auch nicht an einer Frage, die inzwischen jemand bearbeitet hat.
        /// </summary>
        [TestMethod]
        public async Task CreatingTwiceYieldsTheSameStock()
        {
            // Ein echter Spieler: der Besitzer ist ein Fremdschluessel. Mit der Marke raeumt ihn demo-entfernen weg.
            var besitzer = Guid.NewGuid();

            using (var spieler = new PlayersController())
            {
                await spieler.InsertAsync(new Quizzer.DataModels.Models.Base.Player { Id = besitzer, Designation = $"{DemoDataSeeder.Marke} Vorratsprobe" });
                await spieler.SaveChangesAsync();
            }

            var erstes = await MinispielVorrat.CreateAsync(besitzer);
            Assert.AreEqual(MinispielVorrat.Arten.Count, erstes.Neu);

            var vorrat = await VorratInDerDatenbankAsync();
            Assert.AreEqual(MinispielVorrat.Arten.Count, vorrat.Count);
            Assert.IsTrue(vorrat.All(q => q.IsShared), "Eine Vorratsfrage ist nicht geteilt.");
            Assert.IsTrue(vorrat.All(q => q.Id == MinispielVorrat.IdFor(q.Kind)), "Eine Vorratsfrage trägt nicht ihre feste Id.");
            Assert.IsTrue(vorrat.All(q => q.OwnerPlayerId == besitzer), "Der Anleger ist nicht Besitzer.");

            using (var fragen = new QuestionBasesController())
            {
                var bearbeitet = (await fragen.GetAsync(MinispielVorrat.IdFor(MinigameKind.Memory)))!;
                bearbeitet.Points = 999;
                await fragen.SaveWithStepsAsync(bearbeitet);
                await fragen.SaveChangesAsync();
            }

            var zweites = await MinispielVorrat.CreateAsync(Guid.NewGuid());

            Assert.AreEqual(0, zweites.Neu, "Das zweite Anlegen hat etwas angelegt.");
            Assert.AreEqual(MinispielVorrat.Arten.Count, zweites.Vorhanden);

            var danach = await VorratInDerDatenbankAsync();
            Assert.AreEqual(vorrat.Count, danach.Count, "Zweimal angelegt ergibt eine andere Anzahl.");
            Assert.AreEqual(999, danach.Single(q => q.Kind == MinigameKind.Memory).Points, "Eine vorhandene Frage wurde überschrieben.");
            Assert.IsTrue(danach.All(q => q.OwnerPlayerId == besitzer), "Der Besitzer hat beim zweiten Anlegen gewechselt.");
        }

        /// <summary><c>demo-entfernen</c> lässt den Vorrat stehen - er trägt keine Marke, und Spiele dürfen darauf verweisen.</summary>
        [TestMethod]
        public async Task RemovingTheDemoLeavesTheStock()
        {
            await MinispielVorrat.CreateAsync(null);
            await DemoDataSeeder.CreateMinispielabendAsync();

            var entfernt = await DemoDataRemover.RemoveAsync();

            Assert.AreEqual(1, entfernt.Spiele, "Die Probe sagt nichts, wenn nichts entfernt wurde.");
            Assert.AreEqual(MinispielVorrat.Arten.Count, (await VorratInDerDatenbankAsync()).Count, "demo-entfernen hat den Vorrat angegriffen.");

            using var kategorien = new CategoriesController();
            Assert.IsNotNull(await kategorien.GetAsync(MinispielVorrat.KategorieId), "Die Kategorie des Vorrats ist weg.");
        }
    }
}
