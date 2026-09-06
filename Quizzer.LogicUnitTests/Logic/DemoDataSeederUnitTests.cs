using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
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
            if (!string.IsNullOrEmpty(datenordnerVorher))
            {
                Quizzer.DataModels.Settings.FilePathQuizzer = datenordnerVorher;
                datenordnerVorher = string.Empty;
            }

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

        /// <summary>
        /// Eine <b>echte</b> Frage in einer Demo-Kategorie ueberlebt das Entfernen.
        /// <para>
        /// <b>Befund B46, gemessen am 2026-09-06.</b> Der Fremdschluessel
        /// <c>FK_QuestionBase_Category_CategoryId</c> steht in der Datenbank auf <c>CASCADE</c>.
        /// Wer beim Durchprobieren eine eigene Frage in eine Demo-Kategorie legt und danach
        /// <c>demo-entfernen</c> aufruft, verlor sie <b>stillschweigend</b> - ohne Rueckfrage,
        /// ohne Meldung, mit Schritten und Ergebniszeilen.
        /// </para>
        /// <para>
        /// Diese Zusicherung war vor der Behebung rot: die Frage kam als <c>null</c> zurueck.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task ARealQuestionInADemoCategorySurvivesTheRemoval()
        {
            var e = await DemoDataSeeder.CreateAsync();

            using var katCtrl = new CategoriesController();

            var demokategorie = (await katCtrl.GetAllAsync())
                .First(c => c.Designation.StartsWith(DemoDataSeeder.Marke, StringComparison.Ordinal));

            var eigene = new DefaultQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Meine eigene Frage",
                DesignationShort = "EIG",
                CategoryId = demokategorie.Id,
                Points = 100,
            };

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.InsertAsync(eigene);
                await ctrl.SaveChangesAsync();
            }

            try
            {
                var entfernt = await DemoDataRemover.RemoveAsync();

                using var fragenCtrl = new QuestionBasesController();

                Assert.IsNotNull(await fragenCtrl.GetAsync(eigene.Id),
                    "Die eigene Frage wurde beim Entfernen der Demodaten mitgeloescht. Genau das "
                    + "macht der CASCADE-Fremdschluessel auf die Kategorie - lautlos.");

                Assert.AreEqual(1, entfernt.BehalteneKategorien,
                    "Die belegte Demo-Kategorie wurde nicht als behalten gemeldet. Dann erfaehrt "
                    + "der Spielleiter nicht, warum ein Rest stehen blieb.");

                Assert.AreEqual(3, entfernt.Kategorien,
                    "Die drei leeren Demo-Kategorien haetten weggeraeumt werden muessen.");
            }
            finally
            {
                using (var ctrl = new QuestionBasesController())
                {
                    await ctrl.DeleteAsync(eigene.Id);
                    await ctrl.SaveChangesAsync();
                }

                using var aufraeumen = new CategoriesController();

                foreach (var rest in (await aufraeumen.GetAllAsync())
                         .Where(c => c.Designation.StartsWith(DemoDataSeeder.Marke, StringComparison.Ordinal)))
                {
                    await aufraeumen.DeleteAsync(rest.Id);
                }

                await aufraeumen.SaveChangesAsync();
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
        /// <summary>
        /// Kein Demo-Startbildschirm verrät die Frage.
        /// <para>
        /// <b>Die Spielregel</b> (Nutzerwort vom 2026-09-06): der Spielleiter liest die Frage
        /// vor, und wer buzzert, bevor sie zu Ende gelesen ist, darf sie nicht lesen können. Auf
        /// dem ersten Beamerbildschirm steht deshalb nur die Fragenart.
        /// </para>
        /// <para>
        /// <b>Und genau daran scheiterte der Demoabend.</b> Bis zum 2026-09-06 übergab der Seeder
        /// den Fragetext als Text des Startschritts - bei allen zwölf Fragen, wörtlich. Die
        /// Schritt-Ansicht darunter hätte ihn gezeichnet, und die Regel wäre am ersten Abend
        /// ausgehebelt gewesen, ohne dass es jemandem auffiele.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task NoDemoStartScreenGivesTheQuestionAway()
        {
            await DemoDataSeeder.CreateAsync();

            using var ctrl = new QuestionBasesController();

            var verraeter = new List<string>();
            var geprueft = 0;

            foreach (var kurz in (await ctrl.GetAllAsync())
                     .Where(q => q.Designation.StartsWith(DemoDataSeeder.Marke, StringComparison.Ordinal)))
            {
                var frage = await ctrl.GetAsync(kurz.Id);

                Assert.IsNotNull(frage, $"Die Frage {kurz.Designation} liess sich nicht laden.");

                frage!.CalculateOrderdSteps();

                geprueft++;

                var erster = frage.OrderedSteps.FirstOrDefault();

                if (erster == null || !erster.IsStart)
                {
                    verraeter.Add($"{frage.Designation}: der erste Bildschirm ist kein Startschritt");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(frage.QuestionText)
                    && (erster.StepText ?? string.Empty).Contains(frage.QuestionText, StringComparison.Ordinal))
                {
                    verraeter.Add($"{frage.Designation}: der Startschritt traegt den Fragetext");
                }
            }

            Assert.AreEqual(12, geprueft, "Es wurden nicht alle zwoelf Demofragen geprueft.");

            Assert.AreEqual(0, verraeter.Count,
                "Diese Demofragen zeigen die Frage schon auf dem ersten Beamerbildschirm:"
                + Environment.NewLine + string.Join(Environment.NewLine, verraeter));
        }

        /// <summary>
        /// Bilderrunde und Musikrunde: zwei Demofragen tragen ein Medium, und zwar auf einem
        /// normalen Schritt - nicht auf dem Startschritt.
        /// <para>
        /// <b>Nutzerentscheidung vom 2026-09-06 (Frage F06).</b> Gemessen an der Spieldatenbank
        /// trugen die zwoelf Demofragen null Medien. Der Abend, der zum Durchprobieren gedacht
        /// war, fuhr also weder Bild noch Ton an.
        /// </para>
        /// <para>
        /// Der Test setzt <c>Settings.FilePathQuizzer</c> auf einen eigenen Ordner mit genau
        /// zwei Dateien. Gegen den echten Datenordner zu messen waere von dessen Inhalt abhaengig
        /// und damit keine Messung.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TwoDemoQuestionsCarryAMedium()
        {
            var ordner = MitMedienordner(bild: true, ton: true);

            try
            {
                var bildfrage = DemofrageMitSchritten("Der weiße Hai");
                var tonfrage = DemofrageMitSchritten("Das Instrument");
                var andere = DemofrageMitSchritten("Planet der Ringe");

                DemoDataSeeder.HaengeMediumAn(bildfrage);
                DemoDataSeeder.HaengeMediumAn(tonfrage);
                DemoDataSeeder.HaengeMediumAn(andere);

                var bildschritt = bildfrage.Steps.Single(s => s.HasResource);
                var tonschritt = tonfrage.Steps.Single(s => s.HasResource);

                Assert.AreEqual(ResourceType.Image, bildschritt.ResourceTyp,
                    "Die Bilderrunde traegt kein Bild.");
                Assert.AreEqual(ResourceType.Audio, tonschritt.ResourceTyp,
                    "Die Musikrunde traegt keinen Ton.");

                Assert.IsFalse(bildschritt.IsStart || bildschritt.IsFinish,
                    "Das Bild haengt am Startschritt. Dort liest der Spielleiter erst vor - wer "
                    + "zu frueh buzzert, saehe es sonst.");
                Assert.IsFalse(tonschritt.IsStart || tonschritt.IsFinish,
                    "Der Ton haengt am Startschritt statt an einem normalen Schritt.");

                // Die Gegenrichtung: nur diese beiden bekommen etwas.
                Assert.IsFalse(andere.Steps.Any(s => s.HasResource),
                    "Eine dritte Frage hat ebenfalls ein Medium bekommen - dann haengt es an "
                    + "jeder, und der Test misst die Auswahl gar nicht.");
            }
            finally
            {
                Directory.Delete(ordner, recursive: true);
            }
        }

        /// <summary>
        /// Die zweite Gegenrichtung: liegt im Ressourcenordner nichts Passendes, bleibt der Abend
        /// textlich - und es fliegt nichts.
        /// <para>
        /// Ohne diese Probe waere ungeprueft, ob der Seeder einen erfundenen Dateinamen
        /// einträgt. Der waere schlimmer als gar keiner: es gibt im ganzen Programm keinen
        /// <c>MediaFailed</c>-Behandler, eine unlesbare Datei bliebe also schwarz und stumm.
        /// </para>
        /// </summary>
        [TestMethod]
        public void WithoutFilesNothingIsAttached()
        {
            var ordner = MitMedienordner(bild: false, ton: false);

            try
            {
                var frage = DemofrageMitSchritten("Der weiße Hai");

                DemoDataSeeder.HaengeMediumAn(frage);

                Assert.IsFalse(frage.Steps.Any(s => s.HasResource),
                    "Es wurde ein Medium eingetragen, obwohl keine Datei da ist.");
            }
            finally
            {
                Directory.Delete(ordner, recursive: true);
            }
        }

        /// <summary>
        /// Legt einen eigenen Datenordner an und stellt <c>Settings</c> darauf um. Gibt den Pfad
        /// zurueck, damit der Aufrufer ihn wieder wegraeumt.
        /// </summary>
        private string MitMedienordner(bool bild, bool ton)
        {
            var wurzel = Path.Combine(Path.GetTempPath(), "quizzer-demo-medien-" + Guid.NewGuid());

            Directory.CreateDirectory(Path.Combine(wurzel, "Resources"));

            if (bild)
                File.WriteAllText(Path.Combine(wurzel, "Resources", "probe.png"), "kein echtes Bild");

            if (ton)
                File.WriteAllText(Path.Combine(wurzel, "Resources", "probe.mp3"), "kein echter Ton");

            datenordnerVorher = Quizzer.DataModels.Settings.FilePathQuizzer;
            Quizzer.DataModels.Settings.FilePathQuizzer = wurzel;

            return wurzel;
        }

        private string datenordnerVorher = string.Empty;

        /// <summary>Eine Demofrage mit Start-, Hinweis- und Abschlussschritt.</summary>
        private static QuestionBase DemofrageMitSchritten(string bezeichnung)
        {
            var frage = new QuestionBase
            {
                Id = Guid.NewGuid(),
                Designation = $"{DemoDataSeeder.Marke} {bezeichnung}",
            };

            frage.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(), QuestionBaseId = frage.Id,
                SequenceNumber = 1, StepText = "Start", IsStart = true,
            });

            frage.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(), QuestionBaseId = frage.Id,
                SequenceNumber = 10, StepText = "Hinweis",
            });

            frage.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(), QuestionBaseId = frage.Id,
                SequenceNumber = 900, StepText = "Loesung", IsFinish = true,
            });

            return frage;
        }
    }
}
