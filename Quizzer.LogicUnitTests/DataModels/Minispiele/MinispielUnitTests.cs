using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Schrittbau;
using Quizzer.Logic.Sync;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Fragetyp Minispiel (M35 B2): Profile, Checkliste, Pruefung, Migration, Abgleich.
    /// </summary>
    [TestClass]
    public class MinispielUnitTests
    {
        /// <summary>Eine Mehrheit aus der Maske, mit so vielen Optionen wie angegeben.</summary>
        private static MinigameQuestion Mehrheit(params string[] optionen)
        {
            var frage = (MinigameQuestion)Factory.CreateNewQuestion(QuestionType.Minigame);
            frage.Id = Guid.NewGuid();
            frage.Designation = "Essen";
            frage.DesignationShort = "E";
            frage.CategoryId = Guid.NewGuid();

            var composer = StepComposers.For(QuestionType.Minigame);
            var bild = composer.Lies(frage);

            while (bild.Zeilen.Count < optionen.Length)
                bild.Zeilen.Add(((StepComposerBase)composer).NeueZeile());

            for (var i = 0; i < optionen.Length; i++)
                bild.Zeilen[i].Text = optionen[i];

            composer.Schreib(frage, bild);

            return frage;
        }

        /// <summary>
        /// Die Zusicherungen, die ueber <c>All</c>/<c>Enum.GetValues</c> laufen, erfassen den neuen Typ
        /// wirklich - ein Profil, ein Uebersetzer, eine Entitaet.
        /// </summary>
        [TestMethod]
        public void TheChecklistReachesTheMinigame()
        {
            Assert.IsTrue(QuestionTypeProfiles.All.Any(p => p.Typ == QuestionType.Minigame),
                "Kein Profil - dann laufen die Profil-Zusicherungen am Minispiel vorbei.");
            Assert.IsInstanceOfType<MinispielComposer>(StepComposers.For(QuestionType.Minigame));
            Assert.IsInstanceOfType<MinigameQuestion>(Factory.CreateNewQuestion(QuestionType.Minigame));
            // Ueber den Namen gelesen: die Zahl steht in der Datenbank, Enum-Werte nur anhaengen.
            Assert.AreEqual(8, Convert.ToInt32(Enum.Parse<QuestionType>("Minigame")));
        }

        /// <summary>Jede Art hat ein Profil - die Liste laeuft ueber das Enum, nicht ueber eine Hand-Liste.</summary>
        [TestMethod]
        public void EveryKindHasAProfileWithGermanTexts()
        {
            CollectionAssert.AreEquivalent(
                Enum.GetValues<MinigameKind>().ToList(), MinigameProfiles.All.Select(p => p.Kind).ToList());

            foreach (var profil in MinigameProfiles.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(profil.DisplayName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(profil.Description));
                Assert.IsTrue(profil.Variants.Count > 0, $"{profil.Kind}: keine Spielweise.");
                Assert.IsTrue(profil.Variants.All(v => !string.IsNullOrWhiteSpace(v.RulesText)),
                    $"{profil.Kind}: eine Spielweise ohne Regeltext für den Beamer.");
            }
        }

        /// <summary>Mehrheit ist Art 1, und die Spielweise „Minderheit" liegt dort, wo die Wertung sie sucht.</summary>
        [TestMethod]
        public void MajorityIsKindOneAndItsSecondVariantIsTheMinority()
        {
            Assert.AreEqual(1, Convert.ToInt32(Enum.Parse<MinigameKind>("Majority")));

            var frage = new MinigameQuestion();
            Assert.AreEqual(MinigameKind.Majority, frage.Kind);
            Assert.IsFalse(MehrheitEvaluator.IsMinority(frage));

            frage.Variant = MehrheitEvaluator.MinorityVariant;
            Assert.IsTrue(MehrheitEvaluator.IsMinority(frage));
            StringAssert.Contains(MinigameProfiles.For(MinigameKind.Majority).VariantFor(frage.Variant).Name, "Minderheit");
            StringAssert.Contains(MinigameProfiles.RulesFor(frage), "kleinsten");
        }

        /// <summary>Der Aufdeckbildschirm sagt, welches Spiel kommt.</summary>
        [TestMethod]
        public void TheRevealScreenNamesTheKind()
        {
            Assert.AreEqual("Minispiel: Mehrheit", new MinigameQuestion().TypDisplayName);
        }

        /// <summary>Jeder waehlt genau eine Option - abgeleitet vom Uebersetzer, nicht eingegeben.</summary>
        [TestMethod]
        public void EveryPlayerChoosesExactlyOneOption()
        {
            var frage = Mehrheit("Pizza", "Pasta", "Sushi");
            frage.BuzzerMaxAllowedKeySelect = 3;

            var composer = StepComposers.For(QuestionType.Minigame);
            composer.Schreib(frage, composer.Lies(frage));

            Assert.AreEqual(1, frage.BuzzerMaxAllowedKeySelect);
            Assert.AreEqual(3, frage.Steps.Count);
        }

        /// <summary>V2: eine Mehrheit mit nur einer Option ist ein Fehler - schon beim Speichern.</summary>
        [TestMethod]
        public void AMajorityWithOneOptionIsAnError()
        {
            var fehler = QuestionValidator.Validate(Mehrheit("Pizza"))
                .SingleOrDefault(i => i.Code == MinigameChecks.TooFewOptions);

            Assert.IsNotNull(fehler, "Eine einzige Option wird nicht beanstandet.");
            Assert.IsTrue(fehler.IsError);
            StringAssert.Contains(fehler.Message, "mindestens 2");
        }

        /// <summary>Die Gegenrichtung: zwei Optionen genuegen, und sonst meldet die Pruefung nichts.</summary>
        [TestMethod]
        public void AMajorityWithTwoOptionsIsSavable()
        {
            var frage = Mehrheit("Pizza", "Pasta");

            Assert.IsTrue(QuestionValidator.IsSavable(frage),
                string.Join(" | ", QuestionValidator.Validate(frage).Select(i => i.Code)));
            Assert.AreEqual(0, QuestionValidator.Validate(frage).Count,
                "Ohne Abschlusstext soll nichts gewarnt werden - die Auflösung ist die Abstimmung.");
        }

        /// <summary>V2: zu wenige Mitspieler meldet erst der Spielstart; genug meldet nichts.</summary>
        [TestMethod]
        public void TooFewPlayersAreReportedOnlyBelowTheMinimum()
        {
            var frage = Mehrheit("Pizza", "Pasta");
            var mindestens = MinigameProfiles.For(MinigameKind.Majority).MinPlayers;

            Assert.AreEqual(MinigameChecks.TooFewPlayers,
                MinigameChecks.ForPlayers(frage, mindestens - 1).Single().Code);
            Assert.AreEqual(0, MinigameChecks.ForPlayers(frage, mindestens).Count());
        }

        /// <summary>Eine Art aus einer neueren Fassung wird beanstandet, statt beim Oeffnen zu werfen.</summary>
        [TestMethod]
        public void AnUnknownKindIsAnErrorNotACrash()
        {
            var frage = Mehrheit("Pizza", "Pasta");
            frage.Kind = (MinigameKind)99;

            Assert.AreEqual(MinigameChecks.KindUnknown, MinigameChecks.Content(frage).Single().Code);
            Assert.AreEqual("Minispiel", frage.TypDisplayName);
        }

        /// <summary>
        /// Die Migration legt nur die neue Tabelle an. Eine Spalte an einer bestehenden Tabelle bekaeme
        /// eine Vorgabe fuer jede vorhandene Zeile - genau dort haette B1 fast alle Spiele umgestellt.
        /// </summary>
        [TestMethod]
        public void TheMigrationTouchesNoExistingRow()
        {
            var migration = new Quizzer.Logic.Migrations.M35_Minispiel { ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer" };

            var tabelle = migration.UpOperations.OfType<CreateTableOperation>().Single();
            Assert.AreEqual(nameof(MinigameQuestion), tabelle.Name);
            Assert.AreEqual("question", tabelle.Schema);

            var fremd = migration.UpOperations.Where(o => o is not CreateTableOperation).ToList();
            Assert.AreEqual(0, fremd.Count,
                "Die Migration fasst mehr an als die neue Tabelle: " + string.Join(", ", fremd.Select(o => o.GetType().Name)));
        }

        /// <summary>Jeder Fragetyp wandert im Abgleich - eine fehlende Untertabelle wuerde abgewiesen.</summary>
        [TestMethod]
        public void EveryQuestionTypeIsPartOfTheSync()
        {
            foreach (var profil in QuestionTypeProfiles.All)
            {
                var typ = Factory.CreateNewQuestion(profil.Typ).GetType();

                Assert.AreEqual(typ, SyncModel.For(typ).ClrType, $"{profil.Typ} fehlt in SyncModel.Entries.");
            }
        }

        /// <summary>
        /// Je Art das Abgleichformat, mit dem sie kam (M35 C1). Ein altes WPF kennt eine neue Art nicht,
        /// haelt ihr Layout fuer verbogen und schriebe die Tastenwahl zurueck - also neue Art, neues Format.
        /// Eine Art ohne Eintrag wird hier rot und erinnert ans Hochzaehlen.
        /// </summary>
        [TestMethod]
        public void EveryMinigameKindHasAFormatThatIsNotAhead()
        {
            var seitFormat = new Dictionary<MinigameKind, int>
            {
                [MinigameKind.Majority] = 3,
                [MinigameKind.Reaction] = 4,
                [MinigameKind.TapSprint] = 5,
                [MinigameKind.Stopwatch] = 5,
                [MinigameKind.Duel] = 6,
                [MinigameKind.RobberAndGuard] = 6,
                [MinigameKind.ShareOrTake] = 6,
            };

            var ohne = Enum.GetValues<MinigameKind>().Where(k => !seitFormat.ContainsKey(k)).ToList();

            Assert.AreEqual(0, ohne.Count, "Neue Minispiel-Art ohne Abgleichformat - SyncSnapshot.CurrentFormatVersion "
                + "hochzaehlen und hier eintragen: " + string.Join(", ", ohne));
            // Hoeher darf es sein - spaetere Spalten zaehlen ebenfalls hoch.
            Assert.IsTrue(seitFormat.Values.Max() <= Quizzer.DataModels.Transfer.Sync.SyncSnapshot.CurrentFormatVersion,
                "Das Abgleichformat liegt hinter der neuesten Minispiel-Art.");
        }
    }
}
