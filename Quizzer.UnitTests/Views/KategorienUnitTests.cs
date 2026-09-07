using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Die Kategorienliste - klein, aber folgenreich.
    /// <para>
    /// <b>Ohne Zusicherung bis 2026-09-07.</b> Das Raster legt eine neue Zeile schon beim
    /// Hineinklicken an, und eine ohne Bezeichnung wurde anstandslos gespeichert. Sie stand
    /// danach als <b>leerer Eintrag in der Kategorieauswahl jeder Frage</b> - dieselbe Familie
    /// wie der Mitspieler ohne Namen, der in der Anmeldung vorgewaehlt war.
    /// </para>
    /// </summary>
    [TestClass]
    public class KategorienUnitTests
    {
        private readonly List<Guid> angelegt = new();

        [TestInitialize]
        public void SetUp() => UserPrompt.Current = new RecordingUserPrompt(answer: true);

        [TestCleanup]
        public async Task TearDown()
        {
            using (var ctrl = new CategoriesController())
            {
                foreach (var id in angelegt)
                    await ctrl.DeleteAsync(id);

                await ctrl.SaveChangesAsync();
            }

            UserPrompt.Reset();
        }

        private async Task<List<Category>> AllesAsync()
        {
            using var ctrl = new CategoriesController();

            return (await ctrl.GetAllAsync()).ToList();
        }

        /// <summary>Eine Zeile ohne Bezeichnung wird nicht geschrieben.</summary>
        [TestMethod]
        public async Task AnEmptyRowIsNotSaved()
        {
            var vorher = (await AllesAsync()).Count;

            var vm = new CategoriesViewModel();

            vm.Categories.Add(new Category { Id = Guid.NewGuid(), Designation = "   " });
            vm.Categories.Add(new Category { Id = Guid.NewGuid(), Designation = string.Empty });

            await vm.VMSaveAsync();

            var nachher = await AllesAsync();

            Assert.AreEqual(vorher, nachher.Count,
                "Eine leere Kategorie liegt jetzt in der Datenbank - sie erscheint als leerer "
                + "Eintrag in der Kategorieauswahl jeder Frage.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Eine echte Kategorie wird geschrieben - sonst waere die
        /// Zusicherung oben auch gruen, wenn gar nichts mehr gespeichert wuerde.
        /// </summary>
        [TestMethod]
        public async Task ARealRowIsSaved()
        {
            var name = "Zzz Probe " + Guid.NewGuid().ToString("N")[..8];

            var vm = new CategoriesViewModel();

            var neue = new Category { Id = Guid.NewGuid(), Designation = name };

            vm.Categories.Add(neue);

            await vm.VMSaveAsync();

            angelegt.Add(neue.Id);

            var nachher = await AllesAsync();

            Assert.IsTrue(nachher.Any(c => c.Designation == name),
                "Eine ausgefuellte Kategorie wurde nicht gespeichert - dann laesst sich gar "
                + "keine mehr anlegen.");
        }

        /// <summary>Das Laden holt, was da ist, und faengt dabei nicht doppelt an.</summary>
        [TestMethod]
        public async Task LoadingTwiceDoesNotDouble()
        {
            var vm = new CategoriesViewModel();

            await vm.LoadForTestAsync();

            var ersteZahl = vm.Categories.Count;

            await vm.LoadForTestAsync();

            Assert.AreEqual(ersteZahl, vm.Categories.Count,
                "Nach dem zweiten Laden steht jede Kategorie doppelt in der Liste.");

            Assert.AreEqual((await AllesAsync()).Count, vm.Categories.Count,
                "Die Liste zeigt nicht, was in der Datenbank steht.");
        }
    }
}
