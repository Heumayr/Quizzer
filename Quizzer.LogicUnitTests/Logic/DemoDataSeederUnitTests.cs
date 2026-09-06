using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Demo;

namespace Quizzer.LogicUnitTests.Logic
{
    /// <summary>
    /// Der Demo-Quizabend: legt er ein Spiel an, das sich wirklich spielen laesst, und laesst er
    /// sich rueckstandsfrei wieder entfernen?
    /// <para>
    /// Beides zaehlt. Der Seeder schreibt in die <b>echte</b> Spieldatenbank, wenn ihn jemand
    /// ueber <c>dotnet run --project Quizzer.EF -- demo-anlegen</c> aufruft. Was er dort anlegt,
    /// muss vollstaendig sein - und was er anlegt, muss auch wieder verschwinden.
    /// </para>
    /// </summary>
    [TestClass]
    public class DemoDataSeederUnitTests
    {
        [TestInitialize]
        public void SetUp() => TestDatabase.ClearDiscardedChanges();

        [TestCleanup]
        public async Task TearDown()
        {
            await DemoDataRemover.RemoveAsync();
            TestDatabase.ClearDiscardedChanges();
        }

        /// <summary>Der Aufbau steht: Kategorien, Fragen, Mitspieler, ein volles Raster.</summary>
        [TestMethod]
        public async Task TheDemoEveningIsComplete()
        {
            var e = await DemoDataSeeder.CreateAsync();

            Assert.AreEqual(4, e.Kategorien, "Es fehlen Kategorien.");
            Assert.AreEqual(12, e.Fragen, "Es fehlen Fragen.");
            Assert.AreEqual(5, e.Mitspieler, "Es fehlen Mitspieler.");
            Assert.AreEqual(12, e.Zellen, "Nicht jede Zelle traegt eine Frage.");

            using var ctrl = new GamesController();

            var spiel = await ctrl.GetAsync(e.Spiel.Id);

            Assert.IsNotNull(spiel, "Das Spiel wurde nicht geschrieben.");
            Assert.AreEqual(4, spiel!.Width);
            Assert.AreEqual(3, spiel.Height);
            Assert.AreEqual(12, spiel.GameGridCoordinates.Count, "Das Raster ist unvollstaendig.");
            Assert.AreEqual(4, spiel.PlayerXGames.Count, "Es sind nicht vier Mitspieler zugeordnet.");
            Assert.IsNotNull(spiel.Moderator, "Dem Spiel fehlt der Moderator.");

            Assert.AreEqual(4, spiel.Columns.Count(), "Es fehlen Spaltenkoepfe.");
            Assert.AreEqual(3, spiel.Rows.Count(), "Es fehlen Zeilenkoepfe.");
        }

        /// <summary>
        /// Jede Zelle traegt Punkte. Ohne diese Zusicherung waere ein Raster aus lauter
        /// Null-Punkte-Zellen ein bestandener Test - und genau das ist im Bestand schon einmal
        /// passiert.
        /// </summary>
        [TestMethod]
        public async Task EveryCellIsWorthSomething()
        {
            var e = await DemoDataSeeder.CreateAsync();

            using var ctrl = new GamesController();

            var spiel = await ctrl.GetAsync(e.Spiel.Id);

            foreach (var zelle in spiel!.GameGridCoordinates)
            {
                Assert.IsTrue(zelle.CurrentPoints > 0,
                    $"Die Zelle {zelle.X}/{zelle.Y} ist null Punkte wert.");
            }
        }

        /// <summary>
        /// Jede Frage haelt der Fragepruefung stand - sonst laesst sich der Abend nicht spielen.
        /// Alle vier Fragetypen kommen vor.
        /// </summary>
        [TestMethod]
        public async Task EveryQuestionPassesTheValidator()
        {
            await DemoDataSeeder.CreateAsync();

            using var ctrl = new QuestionBasesController();

            var demofragen = new List<QuestionBase>();

            foreach (var kurz in (await ctrl.GetAllAsync())
                     .Where(q => q.Designation.StartsWith(DemoDataSeeder.Marke, StringComparison.Ordinal)))
            {
                // GetAllAsync laedt die Schritte nicht mit - einzeln nachladen, sonst prueft
                // der Validator eine schrittlose Frage.
                var voll = await ctrl.GetAsync(kurz.Id);

                Assert.IsNotNull(voll, $"Die Frage {kurz.Designation} liess sich nicht laden.");

                demofragen.Add(voll!);
            }

            Assert.AreEqual(12, demofragen.Count, "Es wurden nicht alle Fragen gefunden.");

            var typen = demofragen.Select(f => f.Typ).Distinct().ToList();

            Assert.AreEqual(4, typen.Count,
                "Nicht alle vier Fragetypen kommen vor: " + string.Join(", ", typen));

            var maengel = new List<string>();

            foreach (var frage in demofragen)
            {
                var fehler = QuestionValidator.Validate(frage)
                    .Where(i => i.Severity == ValidationSeverity.Error)
                    .ToList();

                if (fehler.Count > 0)
                    maengel.Add($"{frage.Designation}: {string.Join("; ", fehler.Select(f => f.Message))}");
            }

            Assert.AreEqual(0, maengel.Count,
                "Diese Demofragen wuerden im Spiel nicht funktionieren:" + Environment.NewLine
                + string.Join(Environment.NewLine, maengel));
        }

        /// <summary>
        /// Die Gegenrichtung zum Entfernen: erst steht alles da, danach nichts mehr - und was
        /// nicht die Marke traegt, bleibt unberuehrt.
        /// </summary>
        [TestMethod]
        public async Task RemovingTakesTheDemoAndNothingElse()
        {
            var fremdeKategorie = new Category
            {
                Id = Guid.NewGuid(),
                Designation = "Kategorie von Hand",
            };

            using (var ctrl = new CategoriesController())
            {
                await ctrl.InsertAsync(fremdeKategorie);
                await ctrl.SaveChangesAsync();
            }

            try
            {
                await DemoDataSeeder.CreateAsync();

                var entfernt = await DemoDataRemover.RemoveAsync();

                Assert.AreEqual(1, entfernt.Spiele);
                Assert.AreEqual(12, entfernt.Fragen);
                Assert.AreEqual(5, entfernt.Mitspieler);
                Assert.AreEqual(4, entfernt.Kategorien);

                using var ctrlSpiele = new GamesController();

                Assert.AreEqual(0,
                    (await ctrlSpiele.GetAllAsync())
                        .Count(g => g.Designation.StartsWith(DemoDataSeeder.Marke, StringComparison.Ordinal)),
                    "Es ist ein Demo-Spiel uebrig geblieben.");

                using var ctrlKat = new CategoriesController();

                Assert.IsNotNull(await ctrlKat.GetAsync(fremdeKategorie.Id),
                    "Die fremde Kategorie wurde mitgeloescht - das Entfernen greift zu weit.");
            }
            finally
            {
                using var ctrl = new CategoriesController();

                await ctrl.DeleteAsync(fremdeKategorie.Id);
                await ctrl.SaveChangesAsync();
            }
        }

        /// <summary>Ein zweiter Lauf ueberschreibt nichts, sondern legt einen zweiten Abend an.</summary>
        [TestMethod]
        public async Task ASecondRunAddsASecondEvening()
        {
            var erster = await DemoDataSeeder.CreateAsync();
            var zweiter = await DemoDataSeeder.CreateAsync();

            Assert.AreNotEqual(erster.Spiel.Id, zweiter.Spiel.Id);
            Assert.AreNotEqual(erster.Spiel.Designation, zweiter.Spiel.Designation,
                "Beide Abende heissen gleich - dann sind sie in der Liste nicht zu unterscheiden.");

            using var ctrl = new GamesController();

            Assert.AreEqual(2,
                (await ctrl.GetAllAsync())
                    .Count(g => g.Designation.StartsWith(DemoDataSeeder.Marke, StringComparison.Ordinal)));
        }
    }
}
