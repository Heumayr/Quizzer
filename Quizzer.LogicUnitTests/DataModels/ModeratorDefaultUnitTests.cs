using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Demo;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Jeder darf das Quiz leiten - und das Wegnehmen ist der bewusste Handgriff.
    /// <para>
    /// <b>Nutzerentscheidung vom 2026-09-06:</b> „jeder spieler kann sich als spielleiter
    /// anmelden ... per default ... explizites wegnehmen ist sinnvoller für meinen zweck".
    /// </para>
    /// <para>
    /// <b>Was die alte Richtung angerichtet hat, ist gemessen:</b> von zehn angelegten Personen
    /// standen bei der Anmeldung genau <b>zwei</b> zur Wahl - und beide stammten aus den
    /// Demodaten. Wer seine eigenen Leute suchte, fand keinen einzigen. Wer daraufhin das
    /// Anmeldefenster schloss, bekam nicht etwa eine Meldung, sondern ein Programm, das sich
    /// wortlos beendet - von außen nicht von einem Absturz zu unterscheiden.
    /// </para>
    /// <para>
    /// <b>Die zweite Zusicherung ist die wichtigere.</b> Eine Voreinstellung, die sich nicht
    /// wegnehmen lässt, ist keine Voreinstellung, sondern eine Festlegung - und der Nutzer hat
    /// ausdrücklich das Wegnehmen als den Zweck genannt.
    /// </para>
    /// </summary>
    [TestClass]
    public class ModeratorDefaultUnitTests
    {
        [TestInitialize]
        public void SetUp() => TestDatabase.ClearDiscardedChanges();

        /// <summary>Eine frisch angelegte Person darf leiten, ohne dass jemand etwas setzt.</summary>
        [TestMethod]
        public void ANewPlayerMayModerate()
        {
            var frisch = new Player();

            Assert.IsTrue(frisch.IsModerator,
                "Eine neue Person darf nicht leiten. Dann taucht sie in der Anmeldung nicht auf, "
                + "und wer sie angelegt hat, sucht sie dort vergeblich.");
        }

        /// <summary>
        /// Die Gegenrichtung: wird das Recht ausdrücklich weggenommen, bleibt es weg - über das
        /// Schreiben, das Laden und das Klonen hinweg.
        /// <para>
        /// Der Klon zählt hier besonders: der Schreibweg des Projekts schreibt nie die übergebene
        /// Person, sondern einen Klon von ihr. Was der Klon nicht mitnimmt, wird stillschweigend
        /// nie gespeichert.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task TakingTheRightAwayActuallySticks()
        {
            var ohne = new Player
            {
                Id = Guid.NewGuid(),
                Designation = "Ohne Leitung",
                DisplayName = "Ohne Leitung",
                IsModerator = false,
            };

            Assert.IsFalse(ohne.CloneWithoutReferences().IsModerator,
                "Der Klon traegt das Recht wieder. Dann wird das Wegnehmen nie gespeichert - "
                + "lautlos, denn geschrieben wird immer der Klon.");

            try
            {
                using (var ctrl = new PlayersController())
                {
                    await ctrl.InsertAsync(ohne);
                    await ctrl.SaveChangesAsync();
                }

                using var lesen = new PlayersController();
                var geladen = await lesen.GetAsync(ohne.Id);

                Assert.IsNotNull(geladen, "Die Person liess sich nicht wieder laden.");

                Assert.IsFalse(geladen!.IsModerator,
                    "Nach dem Laden darf sie wieder leiten. Das Wegnehmen haelt also nicht, und "
                    + "niemand kann jemanden von der Anmeldung ausschliessen.");
            }
            finally
            {
                using var aufraeumen = new PlayersController();

                await aufraeumen.DeleteAsync(ohne.Id);
                await aufraeumen.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Der Demo-Quizabend führt den Wechsel des Spielleiters vor - dafür müssen alle fünf
        /// zur Wahl stehen.
        /// </summary>
        [TestMethod]
        public async Task EveryDemoPlayerMayModerate()
        {
            try
            {
                var e = await DemoDataSeeder.CreateAsync();

                using var ctrl = new PlayersController();

                var demo = (await ctrl.GetAllAsync())
                    .Where(p => p.Designation.StartsWith(DemoDataSeeder.Marke, StringComparison.Ordinal))
                    .ToList();

                Assert.AreEqual(e.Mitspieler, demo.Count, "Nicht alle Demo-Personen gefunden.");

                var ohneRecht = demo.Where(p => !p.IsModerator).Select(p => p.Designation).ToList();

                Assert.AreEqual(0, ohneRecht.Count,
                    "Diese Demo-Personen duerfen nicht leiten: " + string.Join(", ", ohneRecht));
            }
            finally
            {
                await DemoDataRemover.RemoveAsync();
                TestDatabase.ClearDiscardedChanges();
            }
        }
    }
}
