using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Die Mitspielerauswahl beim Import — drei Ausgänge, nicht zwei.
    /// <para>
    /// <b>Nutzerwort vom 2026-09-06:</b> „die spieler können beim import gewählt werden (oder
    /// auch keine)". „Ohne Mitspieler" und „abgebrochen" sind ausdrücklich verschieden: das eine
    /// legt ein Spiel an, das andere nicht. Der Unterschied hängt allein daran, dass
    /// <c>Ergebnis</c> einmal eine leere Liste ist und einmal <c>null</c>.
    /// </para>
    /// <para>
    /// <b>Geprüft war das bisher nur eine Ebene höher</b> (über den ausgetauschten Handler in
    /// <c>GameTransferUiUnitTests</c>) — also gegen eine Attrappe, nicht gegen die Maske, die den
    /// Unterschied wirklich herstellt.
    /// </para>
    /// </summary>
    [TestClass]
    public class ImportMitspielerUnitTests
    {
        private readonly List<Guid> angelegt = new();

        [TestInitialize]
        public async Task SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);

            // Eigene Mitspieler statt der Annahme, es laege welche in der Datenbank. Die gab es
            // beim ersten Anlauf naemlich nicht - die anderen Klassen raeumen hinter sich auf,
            // und die Probe mass dann nichts. Erst gemessen, dann geschrieben.
            using var ctrl = new PlayersController();

            foreach (var name in new[] { "Aaa Import", "Bbb Import" })
            {
                var eingefuegt = await ctrl.InsertAsync(new Player
                {
                    Id = Guid.NewGuid(),
                    Designation = name + " " + Guid.NewGuid().ToString("N")[..6],
                });

                angelegt.Add(eingefuegt.Entity.Id);
            }

            await ctrl.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            using (var ctrl = new PlayersController())
            {
                foreach (var id in angelegt)
                    await ctrl.DeleteAsync(id);

                await ctrl.SaveChangesAsync();
            }

            UserPrompt.Reset();
        }

        private static async Task<ImportPlayersViewModel> GeladenAsync()
        {
            var vm = new ImportPlayersViewModel();

            await vm.LoadForTestAsync();

            return vm;
        }

        /// <summary>Vor jeder Entscheidung steht kein Ergebnis - das ist der Abbruchzustand.</summary>
        [TestMethod]
        public async Task BeforeAnyChoiceThereIsNoResult()
        {
            var vm = await GeladenAsync();

            Assert.IsNull(vm.Ergebnis,
                "Schon vor der Wahl steht ein Ergebnis da - ein geschlossenes Fenster gaelte "
                + "dann als Zustimmung.");

            Assert.IsTrue(angelegt.All(id => vm.Spieler.Any(s => s.Id == id)),
                "Die eigenen Mitspieler wurden nicht geladen - dann misst der Rest nichts.");

            Assert.IsTrue(vm.Spieler.All(s => !s.Gewaehlt),
                "Es ist jemand vorgewaehlt - beim Import kaeme er ungefragt ins Spiel.");
        }

        /// <summary>
        /// <b>„Ohne Mitspieler" ergibt eine leere Liste, nicht <c>null</c>.</b> Der Aufrufer legt
        /// darauf ein Spiel an.
        /// </summary>
        [TestMethod]
        public async Task WithoutPlayersYieldsAnEmptyList()
        {
            var vm = await GeladenAsync();

            vm.NoneCommand.Execute(null);

            Assert.IsNotNull(vm.Ergebnis,
                "„Ohne Mitspieler“ ergibt null - der Aufrufer haelt das fuer einen Abbruch und "
                + "legt gar kein Spiel an.");

            Assert.AreEqual(0, vm.Ergebnis!.Count, "Es wurde jemand mitgegeben.");
        }

        /// <summary>
        /// <b>Abbrechen ergibt <c>null</c>.</b> Der Aufrufer legt dann nichts an.
        /// </summary>
        [TestMethod]
        public async Task CancellingYieldsNull()
        {
            var vm = await GeladenAsync();

            // Erst etwas waehlen, damit der Abbruch wirklich etwas zuruecknimmt.
            vm.Spieler.First(s => s.Id == angelegt[0]).Gewaehlt = true;

            vm.CancelCommand.Execute(null);

            Assert.IsNull(vm.Ergebnis,
                "Ein Abbruch ergibt eine Liste - der Aufrufer legt dann trotzdem ein Spiel an.");
        }

        /// <summary>Was angehakt ist, kommt mit - und nur das.</summary>
        [TestMethod]
        public async Task OnlyTheTickedOnesAreCarried()
        {
            var vm = await GeladenAsync();

            var meiner = vm.Spieler.First(s => s.Id == angelegt[0]);

            meiner.Gewaehlt = true;

            vm.AcceptCommand.Execute(null);

            Assert.IsNotNull(vm.Ergebnis);

            Assert.AreEqual(1, vm.Ergebnis!.Count,
                "Es kamen nicht genau die angehakten mit.");

            Assert.AreEqual(meiner.Id, vm.Ergebnis[0]);
        }
    }
}
