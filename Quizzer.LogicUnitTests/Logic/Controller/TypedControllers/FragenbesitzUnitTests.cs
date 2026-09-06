using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.LogicUnitTests.Logic.Controller.TypedControllers
{
    /// <summary>
    /// Was mit einer Frage geschieht, wenn ihr Besitzer verschwindet.
    /// <para>
    /// <b>B11.</b> <c>OwnerPlayerId</c> war eine nackte <c>Guid?</c>-Spalte ohne
    /// Fremdschlüssel. Wurde der Besitzer entfernt, blieb die Kennung stehen und zeigte ins
    /// Leere - und <c>QuestionOwnership.IsVisible</c> lässt nur <c>null</c> oder die
    /// <i>eigene</i> Kennung durch. Die Frage war damit für <b>jeden</b> unsichtbar, ohne
    /// Meldung und ohne Weg zurück; im Programm gibt es keine Maske, die eine verwaiste Kennung
    /// wieder leeren könnte.
    /// </para>
    /// <para>
    /// <b>Warum das Schema und nicht die Aufrufstelle.</b> Es gibt einen zweiten Weg zum selben
    /// Schaden: <c>DemoDataSeeder.Remove</c> löscht die Demo-Mitspieler, ohne
    /// <c>OwnerPlayerId</c> je anzufassen. Ein Riegel im ViewModel deckte ihn nicht ab.
    /// </para>
    /// </summary>
    [TestClass]
    public class FragenbesitzUnitTests
    {
        private Category kategorie = null!;
        private Player besitzer = null!;
        private QuestionBase frage = null!;

        [TestInitialize]
        public async Task SetUp()
        {
            TestDatabase.ClearDiscardedChanges();

            kategorie = new Category { Id = Guid.NewGuid(), Designation = "Besitz" };

            using (var ctrl = new CategoriesController())
            {
                await ctrl.InsertAsync(kategorie);
                await ctrl.SaveChangesAsync();
            }

            besitzer = new Player
            {
                Id = Guid.NewGuid(),
                Designation = "Besitzerin",
                DisplayName = "Besitzerin",
                IsModerator = true,
            };

            using (var ctrl = new PlayersController())
            {
                await ctrl.InsertAsync(besitzer);
                await ctrl.SaveChangesAsync();
            }

            frage = new DefaultQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Frage mit Besitzer",
                DesignationShort = "B",
                CategoryId = kategorie.Id,
                Points = 100,
                OwnerPlayerId = besitzer.Id,
            };

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.UpsertAsync(frage);
                await ctrl.SaveChangesAsync();
            }
        }

        [TestCleanup]
        public void TearDown() => Session.SignOut();

        private static async Task EntferneBesitzerAsync(Guid id)
        {
            using var ctrl = new PlayersController();

            await ctrl.DeleteAsync(id);
            await ctrl.SaveChangesAsync();
        }

        /// <summary>
        /// Verschwindet der Besitzer, fällt die Frage in den gemeinsamen Bestand - sie
        /// verschwindet nicht, und sie bleibt auch nicht verwaist.
        /// </summary>
        [TestMethod]
        public async Task RemovingTheOwnerReturnsTheQuestionToTheCommonPool()
        {
            await EntferneBesitzerAsync(besitzer.Id);

            using var ctrl = new QuestionBasesController();

            var geladen = await ctrl.GetAsync(frage.Id);

            Assert.IsNotNull(geladen,
                "Die Frage ist mit ihrem Besitzer verschwunden - das Loeschen einer Person "
                + "darf keine Fragen mitnehmen.");

            Assert.IsNull(geladen!.OwnerPlayerId,
                "Die Besitzerkennung zeigt jetzt ins Leere. Damit ist die Frage fuer jeden "
                + "unsichtbar, und es gibt keine Maske, die das wieder aufloest.");
        }

        /// <summary>
        /// <b>Die Wirkung, um die es geht.</b> Die Zusicherung darüber prüft eine Spalte; diese
        /// prüft, was der Spielleiter davon sieht - und sie wäre auch dann rot, wenn jemand die
        /// Sichtbarkeitsregel änderte statt des Schemas.
        /// </summary>
        [TestMethod]
        public async Task AfterTheOwnerIsGoneEveryModeratorSeesTheQuestionAgain()
        {
            var jemandAnderes = new Player
            {
                Id = Guid.NewGuid(),
                Designation = "Jemand anderes",
                DisplayName = "Jemand anderes",
                IsModerator = true,
            };

            using (var ctrl = new PlayersController())
            {
                await ctrl.InsertAsync(jemandAnderes);
                await ctrl.SaveChangesAsync();
            }

            Session.SignIn(jemandAnderes);

            using (var ctrl = new QuestionBasesController())
            {
                var vorher = await ctrl.GetAsync(frage.Id);

                Assert.IsFalse(QuestionOwnership.IsVisible(vorher!),
                    "Die Frage war schon vorher fuer andere sichtbar - dann misst dieser Test "
                    + "den Besitzfilter gar nicht.");
            }

            await EntferneBesitzerAsync(besitzer.Id);

            using (var ctrl = new QuestionBasesController())
            {
                var nachher = await ctrl.GetAsync(frage.Id);

                Assert.IsTrue(QuestionOwnership.IsVisible(nachher!),
                    "Die Frage bleibt unsichtbar, obwohl ihr Besitzer nicht mehr existiert.");
            }
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ohne sie wären die beiden obigen auch dann grün, wenn die
        /// Löschregel jede Besitzerkennung leerte - also auch die von Fragen, die einem noch
        /// lebenden Mitspieler gehören.
        /// </summary>
        [TestMethod]
        public async Task RemovingSomebodyElseLeavesTheOwnershipAlone()
        {
            var unbeteiligt = new Player
            {
                Id = Guid.NewGuid(),
                Designation = "Unbeteiligt",
                DisplayName = "Unbeteiligt",
            };

            using (var ctrl = new PlayersController())
            {
                await ctrl.InsertAsync(unbeteiligt);
                await ctrl.SaveChangesAsync();
            }

            await EntferneBesitzerAsync(unbeteiligt.Id);

            using var ctrlFragen = new QuestionBasesController();

            var geladen = await ctrlFragen.GetAsync(frage.Id);

            Assert.AreEqual(besitzer.Id, geladen!.OwnerPlayerId,
                "Der Besitz ging verloren, obwohl jemand ganz anderes entfernt wurde.");
        }
    }
}
