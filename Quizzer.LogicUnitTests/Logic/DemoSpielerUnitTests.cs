using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Demo;

namespace Quizzer.LogicUnitTests.Logic
{
    /// <summary>
    /// Der Demo-Quizabend legt seine fuenf Personen nur einmal an (V13): ein zweiter Lauf nimmt dieselben, und wer
    /// fehlt, kommt einzeln dazu - wie beim Vorrat. Vorher entstanden je Lauf fuenf neue.
    /// </summary>
    [TestClass]
    public class DemoSpielerUnitTests
    {
        private static readonly string[] Namen = ["Anna", "Bert", "Clara", "Dennis", "Moderator"];

        [TestInitialize]
        public void SetUp() => TestDatabase.ClearDiscardedChanges();

        [TestCleanup]
        public async Task TearDown()
        {
            await DemoDataRemover.RemoveAsync();
            TestDatabase.ClearDiscardedChanges();
        }

        private static async Task<List<Player>> DemoPersonenAsync()
        {
            using var ctrl = new PlayersController();

            return (await ctrl.GetAllAsync()).Where(p => p.Designation.StartsWith(DemoDataSeeder.Marke, StringComparison.Ordinal)).ToList();
        }

        private static async Task<HashSet<Guid>> MitspielerAsync(Guid spielId)
        {
            using var ctrl = new PlayerXGamesController();

            return (await ctrl.GetAllAsync()).Where(x => x.GameId == spielId).Select(x => x.PlayerId).ToHashSet();
        }

        [TestMethod]
        public async Task ASecondRunTakesTheSamePeople()
        {
            var erster = await DemoDataSeeder.CreateAsync();
            var zweiter = await DemoDataSeeder.CreateAsync();

            Assert.AreEqual(Namen.Length, erster.Mitspieler, "Der erste Lauf legt nicht alle fuenf an.");
            Assert.AreEqual(0, zweiter.Mitspieler, "Der zweite Lauf meldet neue Mitspieler.");

            var personen = await DemoPersonenAsync();
            CollectionAssert.AreEquivalent(Namen.Select(n => $"{DemoDataSeeder.Marke} {n}").ToList(), personen.Select(p => p.Designation).ToList(),
                "Nicht genau eine Person je Demo-Namen.");

            CollectionAssert.AreEquivalent((await MitspielerAsync(erster.Spiel.Id)).ToList(), (await MitspielerAsync(zweiter.Spiel.Id)).ToList(),
                "Der zweite Abend spielt mit anderen Personen.");
            Assert.AreEqual(personen.Single(p => p.Designation.EndsWith("Moderator")).Id, zweiter.Spiel.ModeratorPlayerId,
                "Der zweite Abend hat einen anderen Spielleiter.");
        }

        /// <summary>Wer schon da ist, bleibt; nur die Fehlenden entstehen - nicht wieder alle fuenf.</summary>
        [TestMethod]
        public async Task OnlyTheMissingAreCreated()
        {
            var anna = new Player { Id = Guid.NewGuid(), Designation = $"{DemoDataSeeder.Marke} Anna", DisplayName = "Anna" };

            using (var ctrl = new PlayersController())
            {
                await ctrl.InsertAsync(anna);
                await ctrl.SaveChangesAsync();
            }

            var e = await DemoDataSeeder.CreateAsync();

            Assert.AreEqual(Namen.Length - 1, e.Mitspieler, "Anna wurde noch einmal gezaehlt oder angelegt.");
            Assert.AreEqual(1, (await DemoPersonenAsync()).Count(p => p.Designation == anna.Designation), "Es gibt zwei Annas.");
            Assert.IsTrue((await MitspielerAsync(e.Spiel.Id)).Contains(anna.Id), "Der Abend spielt nicht mit der vorhandenen Anna.");
        }
    }
}
